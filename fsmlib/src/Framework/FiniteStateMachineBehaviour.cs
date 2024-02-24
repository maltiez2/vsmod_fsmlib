using MaltiezFSM.API;
using MaltiezFSM.Inputs;
using MaltiezFSM.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace MaltiezFSM.Framework;

public delegate (ToolModeSetter, SkillItem[]) ToolModesGetter(ItemSlot slot, IPlayer forPlayer, BlockSelection blockSel);
public delegate string? ToolModeSetter(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode);

public class FiniteStateMachineBehaviour<TAttributesFormat> : CollectibleBehavior, IToolModeEventProvider
    where TAttributesFormat : IBehaviourAttributesParser, new()
{
    public FiniteStateMachineBehaviour(CollectibleObject collObj) : base(collObj)
    {

    }

    public bool PreventAttack { get; set; } = false;
    public bool PreventInteraction { get; set; } = false;


    private IFiniteStateMachine? mFsm;
    private IInputManager? mInputManager;
    private JsonObject? mProperties;
    private readonly List<ISystem> mSystems = new();
    private ICoreAPI? mApi;
    private IToolModeInvoker? mToolModeInvoker;

    public event ToolModesGetter? OnGetToolModes;

    private readonly Dictionary<long, List<(int size, ToolModeSetter setter)>> mToolModesConversion = new();
    private readonly Dictionary<long, TimeSpan> mToolModesUpdatesCooldown = new();
    private readonly Dictionary<long, SkillItem[]> mToolModesCache = new();
    private readonly TimeSpan mToolModesCooldown = TimeSpan.FromSeconds(3);

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        mApi = api;

        if (mProperties == null)
        {
            Logger.Error(api, this, $"Null behavior properties on initializing FSM for: {collObj.Code}");
            return;
        }

        if (mProperties.KeyExists("PreventAttack"))
        {
            PreventAttack = mProperties["PreventAttack"].AsBool();
        }

        if (mProperties.KeyExists("PreventInteraction"))
        {
            PreventInteraction = mProperties["PreventInteraction"].AsBool();
        }

        Logger.Debug(api, this, $"Initializing FSM for: {collObj.Code}");

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

        mInputManager = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputManager();
        IOperationInputInvoker? operationInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetOperationInputInvoker();

        IBehaviourAttributesParser parser = new TAttributesFormat();
        bool successfullyParsed = parser.ParseDefinition(api, modSystem.GetOperationFactory(), modSystem.GetSystemFactory(), modSystem.GetInputFactory(), mProperties, collObj);

        if (!successfullyParsed) return;

        try
        {
            mFsm = new FiniteStateMachine(
                api,
                parser.GetOperations(),
                parser.GetSystems(),
                parser.GetInputs(),
                collObj,
                operationInputInvoker,
                new StateManager(api, mProperties)
            );
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating FSM for collectible '{collObj.Code}'.");
            Logger.Verbose(api, this, $"Exception on instantiating FSM for collectible '{collObj.Code}'.\n\nException:\n{exception}\n");
            return;
        }


        Dictionary<string, IOperation> operations = parser.GetOperations();

        foreach ((string code, IInput input) in parser.GetInputs())
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
                mInputManager?.RegisterInput(input, mFsm.Process, collObj);
            }
            catch (Exception exception)
            {
                Logger.Error(api, this, $"Exception on registering input '{Utils.GetTypeName(input.GetType())}' with code '{code}' for collectible '{collObj.Code}'.");
                Logger.Verbose(api, this, $"Exception on registering input '{Utils.GetTypeName(input.GetType())}' with code '{code}' for collectible '{collObj.Code}'.\n\nException:\n{exception}\n");
            }

        }

        foreach ((string code, ISystem system) in parser.GetSystems())
        {
            mSystems.Add(system);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);

        mInputManager?.Dispose();
        mFsm?.Dispose();
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        mProperties = properties;
    }

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
}
