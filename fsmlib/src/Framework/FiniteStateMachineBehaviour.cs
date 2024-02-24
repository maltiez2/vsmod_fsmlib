using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

public delegate (ToolModeSetter, SkillItem[]) ToolModesGetter(ItemSlot slot, IPlayer forPlayer, BlockSelection blockSel);
public delegate string? ToolModeSetter(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode);

public class FiniteStateMachineBehaviour : CollectibleBehavior, IToolModeEventProvider, IFsmBehavior
{
    public FiniteStateMachineBehaviour(CollectibleObject collObj) : base(collObj)
    {

    }

    public int FsmId { get; private set; } = -1;
    public IStateManager? StateManager { get; private set; }

    private FiniteStateMachine? mFsm;
    private JsonObject? mProperties;
    private ICoreAPI? mApi;

    #region Initializing/deinitialising
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        mApi = api;

        if (mProperties == null)
        {
            Logger.Error(api, this, $"Null behavior properties on initializing FSM for: {collObj.Code}");
            return;
        }

        FsmId = mProperties["id"].AsInt(0);
        if (mProperties.KeyExists("PreventAttack")) PreventAttack = mProperties["PreventAttack"].AsBool();
        if (mProperties.KeyExists("PreventInteraction")) PreventInteraction = mProperties["PreventInteraction"].AsBool();

        Logger.Verbose(api, this, $"Initializing FSM for: {collObj.Code}");

        FiniteStateMachineSystem modSystem = api.ModLoader.GetModSystem<FiniteStateMachineSystem>();

        mToolModeInvoker = modSystem.GetToolModeInvoker();

        try
        {
            modSystem.GetAttributeReferencesManager()?.Substitute(mProperties, collObj);
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on substituting 'FromAttr' values '{collObj.Code}'.");
            Logger.Verbose(api, this, $"Exception on substituting 'FromAttr' values '{collObj.Code}'.\n\nException:\n{exception}\n");
        }

        IOperationInputInvoker? operationInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetOperationInputInvoker();

        bool successfullyParsed = ParseDefinition(api, mProperties, collObj, out Dictionary<string, IOperation> operations, out Dictionary<string, ISystem> systems, out Dictionary<string, IInput> inputs);

        if (!successfullyParsed)
        {
            Logger.Verbose(api, this, $"Aborting initializing FSM for: {collObj.Code}");
            return;
        }

        try
        {
            StateManager = new StateManager(api, mProperties, FsmId);
            mFsm = new FiniteStateMachine(
                api,
                operations,
                systems,
                inputs,
                collObj,
                operationInputInvoker,
                StateManager
            );
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating FSM for collectible '{collObj.Code}'.");
            Logger.Verbose(api, this, $"Exception on instantiating FSM for collectible '{collObj.Code}'.\n\nException:\n{exception}\n");
            Logger.Verbose(api, this, $"Aborting initializing FSM for: {collObj.Code}");
            return;
        }

        RegisterInputs(api, operations, inputs);
        SetSystemsForGui(systems);

        mProperties = null;
    }

    public bool ParseDefinition(
        ICoreAPI api,
        JsonObject behaviourAttributes,
        CollectibleObject collectible,
        out Dictionary<string, IOperation> operations,
        out Dictionary<string, ISystem> systems,
        out Dictionary<string, IInput> inputs
        )
    {
        FiniteStateMachineSystem modSystem = api.ModLoader.GetModSystem<FiniteStateMachineSystem>();

        IFactory<IOperation>? operationTypes = modSystem.GetOperationFactory();
        IFactory<ISystem>? systemTypes = modSystem.GetSystemFactory();
        IFactory<IInput>? inputTypes = modSystem.GetInputFactory();

        Dictionary<string, IOperation> operationsLocal = new();
        Dictionary<string, ISystem> systemsLocal = new();
        Dictionary<string, IInput> inputsLocal = new();

        operations = operationsLocal;
        systems = systemsLocal;
        inputs = inputsLocal;

        if (systemTypes == null || operationTypes == null || inputTypes == null) return false;

        try
        {
            Utils.Iterate(behaviourAttributes["systems"], (code, definition) => AddObject(api, "System", code, definition, collectible, systemTypes, systemsLocal));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating a system for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating a system for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        try
        {
            Utils.Iterate(behaviourAttributes["operations"], (code, definition) => AddObject(api, "Operation", code, definition, collectible, operationTypes, operationsLocal));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating an operation for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating an operation for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        try
        {
            Utils.Iterate(behaviourAttributes["inputs"], (code, definition) => AddObject(api, "Input", code, definition, collectible, inputTypes, inputsLocal));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating an input for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating an input for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }



        return true;
    }

    private void AddObject<TObjectInterface>(
        ICoreAPI api,
        string objectType,
        string objectCode,
        JsonObject definition,
        CollectibleObject collectible,
        IFactory<TObjectInterface> factory,
        Dictionary<string, TObjectInterface> container
        )
    {
        string? objectClass = definition["class"].AsString(null);
        if (objectClass == null)
        {
            Logger.Error(api, this, $"{objectType} '{objectCode}' in '{collectible.Code}' has no class specified.");
            return;
        }
        TObjectInterface? objectInstance = factory.Instantiate(objectCode, objectClass, definition, collectible);
        if (objectInstance != null) container.Add(objectCode, objectInstance);
    }

    private void RegisterInputs(ICoreAPI api, Dictionary<string, IOperation> operations, Dictionary<string, IInput> inputs)
    {
        IInputManager? inputManager = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputManager();

        if (mFsm == null || inputManager == null) return;


        foreach ((string code, IInput input) in inputs)
        {
            if (input is IOperationInput operationInput)
            {
                if (operations.ContainsKey(operationInput.OperationCode))
                {
                    operationInput.Operation = operations[operationInput.OperationCode];
                }
                else
                {
                    Logger.Warn(api, this, $"Operation '{operationInput.OperationCode}' from input '{input}' is not found.");
                    continue;
                }
            }

            try
            {
                inputManager.RegisterInput(input, mFsm.Process, collObj);
            }
            catch (Exception exception)
            {
                Logger.Error(api, this, $"Exception on registering input '{Utils.GetTypeName(input.GetType())}' with code '{code}' for collectible '{collObj.Code}'.");
                Logger.Verbose(api, this, $"Exception on registering input '{Utils.GetTypeName(input.GetType())}' with code '{code}' for collectible '{collObj.Code}'.\n\nException:\n{exception}\n");
            }

        }
    }
    private void SetSystemsForGui(Dictionary<string, ISystem> systems)
    {
        foreach ((_, ISystem system) in systems)
        {
            mSystems.Add(system);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        mFsm?.Dispose();
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        mProperties = properties;
    }
    #endregion

    #region Providing info to GUI
    private readonly List<ISystem> mSystems = new();

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        foreach (ISystem system in mSystems)
        {
            string[]? descriptions = system.GetDescription(inSlot, world);

            if (descriptions == null) continue;

            foreach (string description in descriptions)
            {
                dsc.Append(description);
            }
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        List<WorldInteraction> interactionsHelp = base.GetHeldInteractionHelp(inSlot, ref handling).ToList();

        if (mFsm == null) return interactionsHelp.ToArray();

        foreach (IInput input in mFsm.GetAvailableInputs(inSlot))
        {
            try
            {
                WorldInteraction? interactionHelp = input.GetInteractionInfo(inSlot);
                if (interactionHelp != null) interactionsHelp.Add(interactionHelp);
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"Exception on getting held interaction help for '{Utils.GetTypeName(input.GetType())}' input for collectible '{collObj.Code}'.");
                Logger.Verbose(mApi, this, $"Exception on getting held interaction help for '{Utils.GetTypeName(input.GetType())}' input for collectible '{collObj.Code}'.\n\nException:\n{exception}\n");
            }
        }

        return interactionsHelp.ToArray();
    }
    #endregion

    #region Managing collectible interations/attacks
    public bool PreventAttack { get; set; } = false;
    public bool PreventInteraction { get; set; } = false;

    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (PreventAttack)
        {
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }
    }

    public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, ref EnumHandling handling) => false;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (PreventInteraction)
        {
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling) => false;
    #endregion

    #region Managing tool modes
    public event ToolModesGetter? OnGetToolModes;

    private IToolModeInvoker? mToolModeInvoker;
    private readonly Dictionary<long, List<(int size, ToolModeSetter setter)>> mToolModesConversion = new();
    private readonly Dictionary<long, TimeSpan> mToolModesUpdatesCooldown = new();
    private readonly Dictionary<long, SkillItem[]> mToolModesCache = new();
    private readonly TimeSpan mToolModesCooldown = TimeSpan.FromSeconds(3);

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        if (OnGetToolModes == null || slot?.Itemstack?.Collectible == null || forPlayer == null) return Array.Empty<SkillItem>();

        return UpdateToolModes(slot, forPlayer, blockSel);
    }

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
    {
        if (mApi?.Side == EnumAppSide.Server) UpdateToolModes(slot, byPlayer, blockSelection);

        long entityId = byPlayer.Entity.EntityId;
        if (!mToolModesConversion.ContainsKey(entityId)) return;

        int lastIndex = 0;
        foreach ((int size, ToolModeSetter setter) in mToolModesConversion[entityId])
        {
            if (lastIndex <= toolMode)
            {
                CallSetToolModeSetter(slot, byPlayer, blockSelection, toolMode - lastIndex, setter);
                break;
            }

            lastIndex += size;
        }
    }

    private SkillItem[] UpdateToolModes(ItemSlot slot, IPlayer forPlayer, BlockSelection blockSel)
    {
        long entityId = forPlayer.Entity.EntityId;
        TimeSpan timeElapsed = TimeSpan.FromMilliseconds(mApi?.World.ElapsedMilliseconds ?? 0);

        if (mToolModesUpdatesCooldown.ContainsKey(entityId))
        {
            TimeSpan previousTime = mToolModesUpdatesCooldown[entityId];
            if (timeElapsed - previousTime < mToolModesCooldown)
            {
                if (mToolModesCache.ContainsKey(entityId))
                {
                    return mToolModesCache[entityId];
                }

                return Array.Empty<SkillItem>();
            }
        }

        mToolModesUpdatesCooldown[entityId] = timeElapsed;

        IEnumerable<SkillItem[]> result = new List<SkillItem[]>();

        if (OnGetToolModes == null)
        {
            return Array.Empty<SkillItem>();
        }

        if (mToolModesConversion.ContainsKey(entityId))
        {
            mToolModesConversion[entityId].Clear();
        }
        else
        {
            mToolModesConversion[entityId] = new();
        }

        foreach (ToolModesGetter handler in OnGetToolModes.GetInvocationList().OfType<ToolModesGetter>())
        {
            try
            {
                (ToolModeSetter setter, SkillItem[] modes) = handler(slot, forPlayer, blockSel);
                result = result.Append(modes);
                mToolModesConversion[entityId].Add((modes.Length, setter));
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"Exception while getting tool modes for collectible '{slot.Itemstack.Collectible}' with code '{slot.Itemstack.Item?.Code ?? slot.Itemstack.Block?.Code}'.");
                Logger.Verbose(mApi, this, $"Exception while getting tool modes for collectible '{slot.Itemstack.Collectible}' with code '{slot.Itemstack.Item?.Code ?? slot.Itemstack.Block?.Code}'.\nException: {exception}\n");
            }
        }

        mToolModesCache[entityId] = result.SelectMany(list => list).ToArray();

        return mToolModesCache[entityId];
    }

    private void CallSetToolModeSetter(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode, ToolModeSetter setter)
    {
        string? id;

        try
        {
            id = setter(slot, byPlayer, blockSelection, toolMode);
        }
        catch (Exception exception)
        {
            Logger.Error(mApi, this, $"Exception while setting tool mode '{toolMode}' for collectible '{slot.Itemstack.Collectible}' with code '{slot.Itemstack.Item?.Code ?? slot.Itemstack.Block?.Code}'.");
            Logger.Verbose(mApi, this, $"Exception while setting tool mode '{toolMode}' for collectible '{slot.Itemstack.Collectible}' with code '{slot.Itemstack.Item?.Code ?? slot.Itemstack.Block?.Code}'.\nException: {exception}\n");
            return;
        }

        try
        {
            if (id != null) mToolModeInvoker?.Invoke(slot, byPlayer, id);
        }
        catch (Exception exception)
        {
            Logger.Error(mApi, this, $"Exception while invoking tool mode input for tool mode '{id}' for collectible '{slot.Itemstack.Collectible}' with code '{slot.Itemstack.Item?.Code ?? slot.Itemstack.Block?.Code}'.");
            Logger.Verbose(mApi, this, $"Exception while invoking tool mode input for tool mode '{id}' for collectible '{slot.Itemstack.Collectible}' with code '{slot.Itemstack.Item?.Code ?? slot.Itemstack.Block?.Code}'.\nException: {exception}\n");
        }
    }
    #endregion
}
