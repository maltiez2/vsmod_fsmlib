using HarmonyLib;
using MaltiezFSM.API;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace MaltiezFSM;

public class FiniteStateMachineSystem : ModSystem, IRegistry
{
    private IFactory<IOperation>? mOperationFactory;
    private IFactory<ISystem>? mSystemFactory;
    private IFactory<IInput>? mInputFactory;
    private IInputManager? mInputManager;
    private IOperationInputInvoker? mOperationInputInvoker;
    private ICustomInputInvoker? mCustomInputInvoker;
    private IAttributeReferencesManager? mAttributeReferencesManager;
    private Framework.ToolModeInputInvoker? mToolModeInvoker;
    private Systems.ParticleEffectsManager? mParticleEffectsManager;
    private Systems.SoundEffectsManager? mSoundEffectsManager;
    private readonly List<IInputInvoker> mInputInvokers = new();
    private ICoreAPI? mApi;


    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        mApi = api;

        if (api is ICoreClientAPI clientApiForImGuiDebugWindow)
        {
            Framework.ImGuiDebugWindow.Init(clientApiForImGuiDebugWindow);
        }

        Framework.Logger.Init(api.Logger);
        Framework.Logger.Debug(api, this, "Stared initializing");

        Patch();
        if (api.Side == EnumAppSide.Server) PatchServer();

        api.RegisterEntity("AdvancedEntityProjectile", typeof(Systems.AdvancedEntityProjectile));
        api.RegisterCollectibleBehaviorClass("FiniteStateMachine", typeof(Framework.FiniteStateMachineBehaviour<Framework.BehaviourAttributesParser>));
        api.RegisterCollectibleBehaviorClass("FSMAdvancedProjectile", typeof(Systems.AdvancedProjectileBehavior));

        api.RegisterEntityBehaviorClass("constresist", typeof(Additional.EntityBehaviorConstResists<Additional.ConstResist>));
        api.RegisterEntityBehaviorClass("tempresists", typeof(Additional.EntityBehaviorResists));
        api.RegisterEntityBehaviorClass("EntityBehaviorAimingAccuracyNoReticle", typeof(Systems.EntityBehaviorAimingAccuracyNoReticle));

        mAttributeReferencesManager = new Framework.AttributeReferencesManager(api);

        mOperationFactory = new Framework.Factory<IOperation>(api, new Framework.UniqueIdGeneratorForFactory(1));
        mSystemFactory = new Framework.Factory<ISystem>(api, new Framework.UniqueIdGeneratorForFactory(2));
        mInputFactory = new Framework.Factory<IInput>(api, new Framework.UniqueIdGeneratorForFactory(3));
        mInputManager = new Framework.InputManager(api);

        RegisterSystems();
        RegisterOperations();
        if (api is ICoreClientAPI clientApi)
        {
            RegisterInputInvokers(clientApi);
        }
        if (api is ICoreServerAPI serverApi)
        {
            RegisterInputInvokers(serverApi);
            serverApi.Event.PlayerJoin += (byPlayer) => AddPlayerBehavior(byPlayer.Entity);
        }
        RegisterInputs();

        Framework.Logger.Debug(api, this, "Finished initializing");
    }
    public override void AssetsLoaded(ICoreAPI api)
    {
        mParticleEffectsManager = new(api);
        mSoundEffectsManager = new(api);
    }

    private void RegisterInputInvokers(ICoreClientAPI api)
    {
        if (mInputManager == null) return;

        Framework.OperationInputInvoker invoker = new();
        mOperationInputInvoker = invoker;
        Framework.KeyInputInvoker keyInput = new(api);
        Framework.StatusInputInvokerClient statusInput = new(api);
        Framework.HotkeyInputInvoker hotkey = new(api);
        Framework.ActiveSlotChangedInputInvoker activeSlotChanged = new(api);
        Framework.CustomInputInvoker customInput = new();
        Framework.SlotInputInvoker slotInput = new(api);
        mToolModeInvoker = new();

        mInputManager.RegisterInvoker(invoker, typeof(IOperationInput));
        mInputManager.RegisterInvoker(keyInput, typeof(IKeyInput));
        mInputManager.RegisterInvoker(keyInput, typeof(IMouseInput));
        mInputManager.RegisterInvoker(slotInput, typeof(ISlotContentInput));
        mInputManager.RegisterInvoker(statusInput, typeof(IStatusInput));
        mInputManager.RegisterInvoker(hotkey, typeof(IHotkeyInput));
        mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedAfter));
        mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedBefore));
        mInputManager.RegisterInvoker(customInput, typeof(ICustomInput));
        mInputManager.RegisterInvoker(mToolModeInvoker, typeof(IToolModeInput));

        mInputInvokers.Add(invoker);
        mInputInvokers.Add(keyInput);
        mInputInvokers.Add(slotInput);
        mInputInvokers.Add(statusInput);
        mInputInvokers.Add(hotkey);
        mInputInvokers.Add(activeSlotChanged);
        mInputInvokers.Add(customInput);

        mCustomInputInvoker = customInput;
    }
    private void RegisterInputInvokers(ICoreServerAPI api)
    {
        if (mInputManager == null) return;

        Framework.OperationInputInvoker invoker = new();
        mOperationInputInvoker = invoker;
        Framework.CustomInputInvoker customInput = new();
        Framework.StatusInputInvokerServer statusInput = new(api);
        Framework.ActiveSlotChangedInputInvoker activeSlotChanged = new(api);
        Framework.SlotInputInvoker slotInput = new(api);
        mToolModeInvoker = new();

        mInputManager.RegisterInvoker(invoker, typeof(IOperationInput));
        mInputManager.RegisterInvoker(slotInput, typeof(ISlotContentInput));
        mInputManager.RegisterInvoker(customInput, typeof(ICustomInput));
        mInputManager.RegisterInvoker(statusInput, typeof(IStatusInput));
        mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedAfter));
        mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedBefore));
        mInputManager.RegisterInvoker(mToolModeInvoker, typeof(IToolModeInput));

        mInputInvokers.Add(invoker);
        mInputInvokers.Add(slotInput);
        mInputInvokers.Add(customInput);
        mInputInvokers.Add(statusInput);
        mInputInvokers.Add(activeSlotChanged);

        mCustomInputInvoker = customInput;
    }
    private void RegisterInputs()
    {
        if (mInputFactory == null) return;

        mInputFactory.Register<Inputs.KeyboardKey>("Key");
        mInputFactory.Register<Inputs.MouseKey>("Mouse");
        mInputFactory.Register<Inputs.BeforeSlotChanged>("SlotChange");
        mInputFactory.Register<Inputs.AfterSlotChanged>("SlotSelected");
        mInputFactory.Register<Inputs.HotkeyInput>("Hotkey");
        mInputFactory.Register<Inputs.OperationStarted>("OperationStarted");
        mInputFactory.Register<Inputs.OperationFinished>("OperationFinished");
        mInputFactory.Register<Inputs.StatusInput>("Status");
        mInputFactory.Register<Inputs.SlotContent>("SlotContent");
        mInputFactory.Register<Inputs.Custom>("Custom");
        mInputFactory.Register<Inputs.ToolMode>("ToolMode");
    }
    private void RegisterSystems()
    {
        if (mSystemFactory == null) return;

        mSystemFactory.Register<Systems.Aiming>("Aiming");
        mSystemFactory.Register<Systems.Block<Additional.EntityBehaviorResists>>("Block");
        mSystemFactory.Register<Systems.ChangeGroup>("ChangeGroup");
        mSystemFactory.Register<Systems.Durability>("Durability");
        mSystemFactory.Register<Systems.ItemStackGiver>("ItemStackGiver");
        mSystemFactory.Register<Systems.Melee>("Melee");
        mSystemFactory.Register<Systems.NoSprint>("NoSprint");
        mSystemFactory.Register<Systems.Parry<Additional.EntityBehaviorResists>>("Parry");
        mSystemFactory.Register<Systems.Particles>("Particles");
        mSystemFactory.Register<Systems.Projectiles>("Projectiles");
        mSystemFactory.Register<Systems.Requirements>("Requirements");
        mSystemFactory.Register<Systems.Sounds>("Sounds");
        mSystemFactory.Register<Systems.Stats>("Stats");
        mSystemFactory.Register<Systems.CameraSettings>("CameraSettings");
        mSystemFactory.Register<Systems.ChangeAttribute>("ChangeAttribute");
        mSystemFactory.Register<Systems.SelectionMatch>("SelectionMatch");

        mSystemFactory.Register<Systems.Attachments>("Attachments");
        mSystemFactory.Register<Systems.ItemAnimation>("ItemAnimation");
        mSystemFactory.Register<Systems.PlayerAnimation>("PlayerAnimation");
        mSystemFactory.Register<Systems.ProceduralItemAnimation>("ProceduralItemAnimation");
        mSystemFactory.Register<Systems.ProceduralPlayerAnimation>("ProceduralPlayerAnimation");
    }
    private void RegisterOperations()
    {
        if (mOperationFactory == null) return;

        mOperationFactory.Register<Operations.Instant>("Instant");
        mOperationFactory.Register<Operations.Delayed>("Delayed");
    }

    public static bool TriggerAfterActiveSlotChanged(ServerEventManager __instance, IServerPlayer player, int fromSlot, int toSlot)
    {
        ActiveSlotChangeEventArgs arg = new(fromSlot, toSlot);

        MulticastDelegate? eventDelegate = (MulticastDelegate?)__instance.GetType()?.GetField("AfterActiveSlotChanged", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(__instance);
        if (eventDelegate != null)
        {
            foreach (Delegate handler in eventDelegate.GetInvocationList())
            {
                EventHelper.InvokeSafe(handler, __instance.Logger, "AfterActiveSlotChanged", player, arg);
            }
        }

        return false;
    }

    private static void Patch()
    {
        new Harmony("fsmlib").Patch(
                    typeof(EntityProjectile).GetMethod("impactOnEntity", AccessTools.all),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(Systems.AdvancedEntityProjectile), nameof(Systems.AdvancedEntityProjectile.ImpactOnEntityPatch)))
                    );
    }
    private static void PatchServer()
    {
        new Harmony("fsmlib").Patch(
                    AccessTools.Method(typeof(ServerEventManager), nameof(ServerEventManager.TriggerAfterActiveSlotChanged)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(FiniteStateMachineSystem), nameof(TriggerAfterActiveSlotChanged)))
                    );
    }
    private static void Unpatch()
    {
        new Harmony("fsmlib").Unpatch(typeof(EntityProjectile).GetMethod("impactOnEntity", AccessTools.all), HarmonyPatchType.Prefix, "fsmlib");
    }
    private static void UnpatchServer()
    {
        new Harmony("fsmlib").Unpatch(AccessTools.Method(typeof(ServerEventManager), nameof(ServerEventManager.TriggerAfterActiveSlotChanged)), HarmonyPatchType.Prefix, "fsmlib");
    }

    internal IFactory<IOperation>? GetOperationFactory() => mOperationFactory;
    internal IFactory<ISystem>? GetSystemFactory() => mSystemFactory;
    internal IFactory<IInput>? GetInputFactory() => mInputFactory;
    internal IInputManager? GetInputManager() => mInputManager;
    internal IOperationInputInvoker? GetOperationInputInvoker() => mOperationInputInvoker;
    internal IAttributeReferencesManager? GetAttributeReferencesManager() => mAttributeReferencesManager;
    internal IToolModeInvoker? GetToolModeInvoker() => mToolModeInvoker;

    public Systems.IParticleEffectsManager? ParticleEffects => mParticleEffectsManager;
    public Systems.ISoundEffectsManager? SoundEffects => mSoundEffectsManager;

    private struct BehaviorAsJsonObj
    {
        public string code;
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        BehaviorAsJsonObj newBehavior = new()
        {
            code = "tempresists"
        };
        JsonObject newBehaviorJson = new(JToken.FromObject(newBehavior));

        foreach (EntityProperties entityType in api.World.EntityTypes)
        {
            if (api.Side.IsServer())
            {
                bool alreadyHas = false;
                foreach (JsonObject behavior in entityType.Server.BehaviorsAsJsonObj)
                {
                    if (behavior["code"].AsString() == newBehavior.code)
                    {
                        alreadyHas = true;
                        break;
                    }
                }
                //if (!alreadyHas) api.Logger.VerboseDebug("[FSMlib] Adding behavior '{0}' to entity '{1}:{2}'", newBehavior.code, entityType.Class, entityType.Code);
                if (!alreadyHas) entityType.Server.BehaviorsAsJsonObj = entityType.Server.BehaviorsAsJsonObj.Prepend(newBehaviorJson).ToArray();
            }
            if (api.Side.IsClient())
            {
                // Do not need this one on client side
                bool alreadyHas = false;
                foreach (JsonObject behavior in entityType.Client.BehaviorsAsJsonObj)
                {
                    if (behavior["code"].AsString() == newBehavior.code)
                    {
                        alreadyHas = true;
                        break;
                    }
                }
                if (!alreadyHas) entityType.Client.BehaviorsAsJsonObj = entityType.Client.BehaviorsAsJsonObj.Prepend(newBehaviorJson).ToArray();
            }
        }
    }
    private static void AddPlayerBehavior(EntityPlayer player)
    {
        // In case 'AssetsFinalize' method failed to add behavior to player.
        if (!player.HasBehavior<Additional.EntityBehaviorResists>()) player.SidedProperties.Behaviors.Insert(0, new Additional.EntityBehaviorResists(player));
        if (!player.HasBehavior<Systems.EntityBehaviorAimingAccuracyNoReticle>()) player.SidedProperties.Behaviors.Insert(0, new Systems.EntityBehaviorAimingAccuracyNoReticle(player));
    }

    public override void Dispose()
    {
        mInputManager?.Dispose();
        foreach (IInputInvoker invoker in mInputInvokers)
        {
            invoker.Dispose();
        }
        Framework.ImGuiDebugWindow.DisposeInstance();
        Unpatch();
        if (mApi?.Side == EnumAppSide.Server) UnpatchServer();

        base.Dispose();
    }

    #region Registering in factories
    public ICustomInputInvoker? CustomInputInvoker => mCustomInputInvoker;
    public void RegisterOperation<TProductClass>(string name, ICoreAPI api, Mod mod) where TProductClass : FactoryProduct, IOperation
    {
        if (!CheckRegisterArguments<TProductClass>(api, mod, "operation")) return;
        bool registered = mOperationFactory?.Register<TProductClass>($"{mod.Info.ModID}:{name}") ?? false;
        if (mOperationFactory != null) LogRegistering<TProductClass, IOperation>(registered, name, api, mod, "operation", mOperationFactory);
    }
    public void RegisterSystem<TProductClass>(string name, ICoreAPI api, Mod mod) where TProductClass : FactoryProduct, ISystem
    {
        if (!CheckRegisterArguments<TProductClass>(api, mod, "system")) return;
        bool registered = mSystemFactory?.Register<TProductClass>($"{mod.Info.ModID}:{name}") ?? false;
        if (mSystemFactory != null) LogRegistering<TProductClass, ISystem>(registered, name, api, mod, "system", mSystemFactory);
    }
    public void RegisterInput<TProductClass>(string name, ICoreAPI api, Mod mod) where TProductClass : FactoryProduct, IStandardInput
    {
        if (!CheckRegisterArguments<TProductClass>(api, mod, "input")) return;
        bool registered = mInputFactory?.Register<TProductClass>($"{mod.Info.ModID}:{name}") ?? false;
        if (mInputFactory != null) LogRegistering<TProductClass, IInput>(registered, name, api, mod, "input", mInputFactory);
    }
    public void RegisterInput<TProductClass, TInputInterface>(string name, IInputInvoker invoker, ICoreAPI api, Mod mod)
        where TProductClass : FactoryProduct, IInput
        where TInputInterface : IInput
    {
        if (!CheckRegisterArguments<TProductClass>(api, mod, "input invoker")) return;
        
        bool invokerRegistered = mInputManager?.RegisterInvoker(invoker, typeof(TInputInterface)) ?? false;
        if (invokerRegistered)
        {
            Framework.Logger.Verbose(api, this, $"({Mod}) Registered input invoker: '{Framework.Utils.GetTypeName(invoker.GetType())}' for '{Framework.Utils.GetTypeName(typeof(TInputInterface))}' inputs.");
        }
        
        bool registered =  mInputFactory?.Register<TProductClass>($"{mod.Info.ModID}:{name}") ?? false;
        if (mInputFactory != null) LogRegistering<TProductClass, IInput>(registered, name, api, mod, "input", mInputFactory);
    }

    private const int cModIdMinimumLength = 4;
    private bool CheckRegisterArguments<TProductClass>(ICoreAPI api, Mod mod, string productType)
    {
        if (api == null) throw new ArgumentNullException(nameof(api), $"You should supply not null 'ICoreAPI' on registering objects in FSM lib");
        
        if (mod == null || mod == Mod)
        {
            Framework.Logger.Error(api, this, $"Error on registering {productType}: you should pass your Mod class into this method.");
            return false;
        }

        if (mod.Info.ModID.Length <= cModIdMinimumLength)
        {
            Framework.Logger.Error(api, this, $"Error on registering {productType} for mod {mod}: mod id should be longer than {cModIdMinimumLength} letters, not '{mod.Info.ModID}'. Stop abbreviating domains and mod-ids!");
            return false;
        }

        bool started = productType switch
        {
            "operation" => mOperationFactory != null,
            "system" => mSystemFactory != null,
            "input" => mInputFactory != null,
            "input invoker" => mInputManager != null && mInputFactory != null,
            _ => true
        };

        if (!started)
        {
            Framework.Logger.Error(api, this, $"Error on registering {productType} '{Framework.Utils.GetTypeName(typeof(TProductClass))}' for mod '{mod}': wrong load order, you should register {productType}s after FSM lib is started.");
            return false;
        }

        return true;
    }
    private void LogRegistering<TProductClass, TProductType>(bool registered, string name, ICoreAPI api, Mod mod, string productType, IFactory<TProductType> factory)
    {
        string productName = $"{mod.Info.ModID}:{name}";
        
        if (registered)
        {
            Framework.Logger.Verbose(api, this, $"({Mod}) Registered {productType}: '{Framework.Utils.GetTypeName(typeof(TProductClass))}' as '{productName}'.");
        }
        else
        {
            Framework.Logger.Warn(api, this, $"({Mod}) Failed to register {productType}: '{Framework.Utils.GetTypeName(typeof(TProductClass))}' as '{productName}' - such {productType} name already used for '{Framework.Utils.GetTypeName(factory.TypeOf(productName))}'.");
        }
    }
    #endregion
}
