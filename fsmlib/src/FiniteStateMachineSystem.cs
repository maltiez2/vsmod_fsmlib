using Vintagestory.API.Common;
using MaltiezFSM.API;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using System.Linq;

namespace MaltiezFSM
{
    public class FiniteStateMachineSystem : ModSystem, IFactoryProvider
    {
        private IFactory<IOperation> mOperationFactory;
        private IFactory<ISystem> mSystemFactory;
        private IFactory<IInput> mInputFactory;
        private IInputManager mInputManager;
        private ITransformManager mTransformManager;
        private IOperationInputInvoker? mOperationInputInvoker;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            Framework.Utils.Logger.Init(api.Logger);

            Framework.Utils.Logger.Notify(this, "Started");

            mTransformManager = new Framework.TransformsManager(api);

            api.RegisterItemClass("NoMelee", typeof(NoMelee));
            api.RegisterItemClass("NoMeleeStrict", typeof(NoMeleeStrict));
            api.RegisterCollectibleBehaviorClass("FiniteStateMachine", typeof(Framework.FiniteStateMachineBehaviour<Framework.BehaviourAttributesParser>));

            api.RegisterEntityBehaviorClass("constresist", typeof(Additional.EntityBehaviorConstResists<Additional.ConstResist>));
            api.RegisterEntityBehaviorClass("tempresists", typeof(Additional.EntityBehaviorResists));

            if (api.Side == EnumAppSide.Server) (api as ICoreServerAPI).Event.PlayerJoin += (byPlayer) => AddPlayerBehavior(byPlayer.Entity);

            mOperationFactory = new Framework.Factory<IOperation, Framework.UniqueIdGeneratorForFactory>(api);
            mSystemFactory = new Framework.Factory<ISystem, Framework.UniqueIdGeneratorForFactory>(api);
            mInputFactory = new Framework.Factory<IInput, Framework.UniqueIdGeneratorForFactory>(api);
            mInputManager = new Framework.InputManager(api);

            RegisterSystems();
            RegisterOperations();
            if (api is ICoreClientAPI clientApi) RegisterInputInvokers(clientApi);
            if (api is ICoreServerAPI serverApi) RegisterInputInvokers(serverApi);
            RegisterInputs();
        }

        private void RegisterInputInvokers(ICoreClientAPI api)
        {
            Framework.KeyInputInvoker keyInput = new(api);
            Framework.StatusInputInvoker statusInput = new(api);
            Framework.DropItemsInputInvoker dropItems = new(api);
            Framework.ActiveSlotChangedInputInvoker activeSlotChanged = new(api);

            mInputManager.RegisterInvoker(keyInput, typeof(IKeyInput));
            mInputManager.RegisterInvoker(keyInput, typeof(IMouseInput));
            mInputManager.RegisterInvoker(statusInput, typeof(IStatusInput));
            mInputManager.RegisterInvoker(dropItems, typeof(IItemDropped));
            mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedAfter));
            mInputManager.RegisterInvoker(activeSlotChanged, typeof(ISlotChangedBefore));
        }

        private void RegisterInputInvokers(ICoreServerAPI api)
        {
            mOperationInputInvoker = new Framework.OperationInputInvoker();

            mInputManager.RegisterInvoker(mOperationInputInvoker as Framework.OperationInputInvoker, typeof(IOperationInput));
        }

        private void RegisterSystems()
        {  
            mSystemFactory.Register<Systems.BasicSoundSystem>("Sound");
            mSystemFactory.Register<Systems.BasicReload>("Reload");
            mSystemFactory.Register<Systems.BasicShooting>("Shooting");
            mSystemFactory.Register<Systems.BasicVariantsAnimation<Systems.TickBasedAnimation>>("VariantsAnimation");
            mSystemFactory.Register<Systems.BasicRequirements>("Requirements");
            mSystemFactory.Register<Systems.BasicTransformAnimation>("TransformAnimation");
            mSystemFactory.Register<Systems.BasicPlayerAnimation>("PlayerAnimation");
            mSystemFactory.Register<Systems.BasicPlayerStats>("PlayerStats");
            mSystemFactory.Register<Systems.BasicParticles>("Particles");
            mSystemFactory.Register<Systems.BasicAim>("Aiming");
            mSystemFactory.Register<Systems.NoSway>("NoSway");
            mSystemFactory.Register<Systems.NoSprint>("NoSprint");
            mSystemFactory.Register<Systems.ChangeGroup>("ChangeGroup");
            mSystemFactory.Register<Systems.BasicMelee>("BasicMelee");
            mSystemFactory.Register<Systems.BasicDurabilityDamage>("DurabilityDamage");
            mSystemFactory.Register<Systems.BasicDurability>("Durability");
            mSystemFactory.Register<Systems.ItemStackGiver>("ItemStackGiver");
            mSystemFactory.Register<Systems.SimpleMelee>("SimpleMelee");
            mSystemFactory.Register<Systems.PlayerAnimation>("ProceduralPlayerAnimation");
            mSystemFactory.Register<Systems.ItemAnimation>("ProceduralItemAnimation");
            mSystemFactory.Register<Systems.BasicParry<Additional.EntityBehaviorResists>>("BasicParry");
            mSystemFactory.Register<Systems.BasicBlock<Additional.EntityBehaviorResists>>("BasicBlock");
            mSystemFactory.Register<Systems.SmoothAnimation>("SmoothAnimation");
        }
        private void RegisterOperations()
        {
            mOperationFactory.Register<Operations.Instant>("Instant");
            mOperationFactory.Register<Operations.Delayed>("Delayed");
            mOperationFactory.Register<Operations.Continuous>("Continuous");
        }

        private void RegisterInputs()
        {
            mInputFactory.Register<Inputs.BasicKey>("Key");
            mInputFactory.Register<Inputs.BasicMouse>("MouseKey");
            mInputFactory.Register<Inputs.BasicHotkey>("Hotkey");
            mInputFactory.Register<Inputs.SlotModified>("SlotModified");
            mInputFactory.Register<Inputs.BasicSlotBefore>("SlotChange");
            mInputFactory.Register<Inputs.BasicSlotAfter>("SlotSelected");
            mInputFactory.Register<Inputs.ItemDropped>("ItemDropped");
            mInputFactory.Register<Inputs.Swimming>("Swimming");
            mInputFactory.Register<Inputs.Blank>("Blank");
        }

        public IFactory<IOperation> GetOperationFactory() => mOperationFactory;
        public IFactory<ISystem> GetSystemFactory() => mSystemFactory;
        public IFactory<IInput> GetInputFactory() => mInputFactory;
        public IInputManager GetInputManager() => mInputManager;
        public ITransformManager GetTransformManager() => mTransformManager;
        public IOperationInputInvoker? GetOperationInputInvoker() => mOperationInputInvoker;

        private struct BehaviorAsJsonObj
        {
            public string code;
        }
        public override void AssetsFinalize(ICoreAPI api)
        {
            BehaviorAsJsonObj newBehavior = new();
            newBehavior.code = "tempresists";
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
                    if (!alreadyHas) api.Logger.VerboseDebug("[FSMlib] Adding behavior '{0}' to entity '{1}:{2}'", newBehavior.code, entityType.Class, entityType.Code);
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
        private void AddPlayerBehavior(EntityPlayer player)
        {
            // In case 'AssetsFinalize' method failed to add behavior to player.
            if (!player.HasBehavior<Additional.EntityBehaviorResists>()) player.SidedProperties.Behaviors.Insert(0, new Additional.EntityBehaviorResists(player));
        }

        public override void Dispose()
        {
            mInputManager.Dispose();

            base.Dispose();
        }
    }
}
