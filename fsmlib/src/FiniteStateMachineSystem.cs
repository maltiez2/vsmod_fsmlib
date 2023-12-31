﻿using HarmonyLib;
using MaltiezFSM.API;
using Newtonsoft.Json.Linq;
using System.Linq;
using Vintagestory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace MaltiezFSM;

public class FiniteStateMachineSystem : ModSystem, IRegistry
{
    private IFactory<IOperation>? mOperationFactory;
    private IFactory<ISystem>? mSystemFactory;
    private IFactory<IInput>? mInputFactory;
    private IInputManager? mInputManager;
    private IOperationInputInvoker? mOperationInputInvoker;
    private ICustomInputInvoker? mCustomInputInvoker;
    private Additional.ParticleEffectsManager? mParticleEffectsManager;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        Framework.Logger.Init(api.Logger);
        Framework.Logger.Debug(api, this, "Stared initializing");

        Patch();

        api.RegisterItemClass("NoMelee", typeof(NoMelee));
        api.RegisterItemClass("NoMeleeStrict", typeof(NoMeleeStrict));
        api.RegisterCollectibleBehaviorClass("FiniteStateMachine", typeof(Framework.FiniteStateMachineBehaviour<Framework.BehaviourAttributesParser>));
        api.RegisterCollectibleBehaviorClass("FSMAdvancedProjectile", typeof(Systems.AdvancedProjectileBehavior));

        api.RegisterEntityBehaviorClass("constresist", typeof(Additional.EntityBehaviorConstResists<Additional.ConstResist>));
        api.RegisterEntityBehaviorClass("tempresists", typeof(Additional.EntityBehaviorResists));

        mOperationFactory = new Framework.Factory<IOperation>(api, new Framework.UniqueIdGeneratorForFactory(1));
        mSystemFactory = new Framework.Factory<ISystem>(api, new Framework.UniqueIdGeneratorForFactory(2));
        mInputFactory = new Framework.Factory<IInput>(api, new Framework.UniqueIdGeneratorForFactory(3));
        mInputManager = new Framework.InputManager(api);

        RegisterSystems();
        RegisterOperations();
        if (api is ICoreClientAPI clientApi)
        {
            RegisterInputInvokers(clientApi);
            // @TODO clientApi.Gui.RegisterDialog(new ItemSelectGuiDialog(clientApi_2))
        }
        if (api is ICoreServerAPI serverApi)
        {
            RegisterInputInvokers();
            serverApi.Event.PlayerJoin += (byPlayer) => AddPlayerBehavior(byPlayer.Entity);
        }
        RegisterInputs();

        Framework.Logger.Debug(api, this, "Finished initializing");
    }
    public override void AssetsLoaded(ICoreAPI api)
    {
        mParticleEffectsManager = new(api);
    }

    private void RegisterInputInvokers(ICoreClientAPI api)
    {
        if (mInputManager == null) return;

        Framework.KeyInputInvoker keyInput = new(api);
        Framework.StatusInputInvoker statusInput = new(api);
        Framework.DropItemsInputInvoker dropItems = new(api);
        Framework.ActiveSlotChangedInputInvoker activeSlotChanged = new(api);
        Framework.CustomInputInvoker customInput = new();

        mInputManager.RegisterInvoker(keyInput, typeof(IKeyInput));
        mInputManager.RegisterInvoker(keyInput, typeof(IMouseInput));
        mInputManager.RegisterInvoker(statusInput, typeof(IStatusInput));
        mInputManager.RegisterInvoker(dropItems, typeof(IItemDropped));
        mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedAfter));
        mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedBefore));
        mInputManager.RegisterInvoker(customInput, typeof(ICustomInput));

        mCustomInputInvoker = customInput;
    }
    private void RegisterInputInvokers()
    {
        if (mInputManager == null) return;

        Framework.OperationInputInvoker invoker = new();
        mOperationInputInvoker = invoker;
        Framework.CustomInputInvoker customInput = new();

        mInputManager.RegisterInvoker(invoker, typeof(IOperationInput));
        mInputManager.RegisterInvoker(customInput, typeof(ICustomInput));

        mCustomInputInvoker = customInput;
    }
    private void RegisterInputs()
    {
        if (mInputFactory == null) return;

        mInputFactory.Register<Inputs.KeyboardKey>("Key");
        mInputFactory.Register<Inputs.MouseKey>("Mouse");
        mInputFactory.Register<Inputs.BeforeSlotChanged>("SlotChange");
        mInputFactory.Register<Inputs.AfterSlotChanged>("SlotSelected");
        mInputFactory.Register<Inputs.ItemDropped>("ItemDropped");
        mInputFactory.Register<Inputs.OperationStarted>("OperationStarted");
        mInputFactory.Register<Inputs.OperationFinished>("OperationFinished");
        mInputFactory.Register<Inputs.StatusInput>("Status");
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
        mOperationFactory.Register<Operations.Continuous>("Continuous");
    }

    private static void Patch()
    {
        new Harmony("fsmlib").Patch(
                    typeof(EntityProjectile).GetMethod("impactOnEntity", AccessTools.all),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(Systems.AdvancedEntityProjectile), nameof(Systems.AdvancedEntityProjectile.ImpactOnEntityPatch)))
                    );
    }
    private static void Unpatch()
    {
        new Harmony("fsmlib").Unpatch(typeof(EntityProjectile).GetMethod("impactOnEntity", AccessTools.all), HarmonyPatchType.Prefix, "fsmlib");
    }

    internal IFactory<IOperation>? GetOperationFactory() => mOperationFactory;
    internal IFactory<ISystem>? GetSystemFactory() => mSystemFactory;
    internal IFactory<IInput>? GetInputFactory() => mInputFactory;
    internal IInputManager? GetInputManager() => mInputManager;
    internal IOperationInputInvoker? GetOperationInputInvoker() => mOperationInputInvoker;

    public Additional.ParticleEffectsManager? ParticleEffects => mParticleEffectsManager;
    public ICustomInputInvoker? CustomInputInvoker => mCustomInputInvoker;
    public void RegisterOperation<TProductClass>(string name) where TProductClass : FactoryProduct, IOperation => mOperationFactory?.Register<TProductClass>(name);
    public void RegisterSystem<TProductClass>(string name) where TProductClass : FactoryProduct, ISystem => mSystemFactory?.Register<TProductClass>(name);
    public void RegisterInput<TProductClass>(string name) where TProductClass : FactoryProduct, IStandardInput => mInputFactory?.Register<TProductClass>(name);
    public void RegisterInput<TProductClass, TInputInterface>(string name, IInputInvoker invoker)
        where TInputInterface : IInput
        where TProductClass : FactoryProduct, IInput
    {
        mInputManager?.RegisterInvoker(invoker, typeof(TInputInterface));
        mInputFactory?.Register<TProductClass>(name);
    }

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
    }

    public override void Dispose()
    {
        mInputManager?.Dispose();
        Unpatch();

        base.Dispose();
    }
}
