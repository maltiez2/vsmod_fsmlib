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

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            Framework.Utils.Logger.Init(api);

            mTransformManager = new Framework.TransformsManager(api);

            api.RegisterItemClass("NoMelee", typeof(NoMelee));
            api.RegisterCollectibleBehaviorClass("FiniteStateMachine", typeof(Framework.FiniteStateMachineBehaviour<Framework.BehaviourAttributesParser, Framework.FiniteStateMachine>));

            api.RegisterEntityBehaviorClass("constresist", typeof(Additional.EntityBehaviorConstResists<Additional.ConstResist>));
            api.RegisterEntityBehaviorClass("tempresists", typeof(Additional.EntityBehaviorResists));

            if (api.Side == EnumAppSide.Server) (api as ICoreServerAPI).Event.PlayerJoin += (byPlayer) => AddPlayerBehavior(byPlayer.Entity);

            mOperationFactory = new Framework.Factory<IOperation, Framework.UniqueIdGeneratorForFactory>(api);
            mSystemFactory = new Framework.Factory<ISystem, Framework.UniqueIdGeneratorForFactory>(api);
            mInputFactory = new Framework.Factory<IInput, Framework.UniqueIdGeneratorForFactory>(api);

            RegisterSystems();
            RegisterOperations();
            RegisterInputs();

            IActiveSlotListener activeSlotListener = (api.Side == EnumAppSide.Client) ? new Framework.ActiveSlotActiveListener(api as ICoreClientAPI) : null;
            IHotkeyInputManager hotkeyInputManager = (api.Side == EnumAppSide.Client) ? new Framework.HotkeyInputManager(api as ICoreClientAPI) : null;
            IStatusInputManager statusInputManager = (api.Side == EnumAppSide.Client) ? new Framework.StatusInputManager(api as ICoreClientAPI) : null;
            IKeyInputManager    keyInputManager    = (api.Side == EnumAppSide.Client) ? new Framework.KeyInputManager(api as ICoreClientAPI) : null;
            ICustomInputManager customInputManager = new Framework.CustomInputManager(api);
            mInputManager = new Framework.InputManager(api, activeSlotListener, hotkeyInputManager, statusInputManager, keyInputManager, customInputManager);
        }

        public void RegisterSystems()
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
            mSystemFactory.Register<Systems.BasicParry<Additional.EntityBehaviorResists>>("BasicParry");
        }
        public void RegisterOperations()
        {
            mOperationFactory.Register<Operations.BasicInstant>("Instant");
            mOperationFactory.Register<Operations.BasicDelayed>("Delayed");
        }

        public void RegisterInputs()
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

        public IFactory<IOperation> GetOperationFactory()
        {
            return mOperationFactory;
        }
        public IFactory<ISystem> GetSystemFactory()
        {
            return mSystemFactory;
        }
        public IFactory<IInput> GetInputFactory()
        {
            return mInputFactory;
        }
        public IInputManager GetInputManager()
        {
            return mInputManager;
        }
        public ITransformManager GetTransformManager()
        {
            return mTransformManager;
        }

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
                    /*bool alreadyHas = false;
                    foreach (JsonObject behavior in entityType.Client.BehaviorsAsJsonObj)
                    {
                        if (behavior["code"].AsString() == newBehavior.code)
                        {
                            alreadyHas = true;
                            break;
                        }
                    }
                    if (!alreadyHas) entityType.Client.BehaviorsAsJsonObj = entityType.Client.BehaviorsAsJsonObj.Prepend(newBehaviorJson).ToArray();*/
                }
            }
        }

        private void AddPlayerBehavior(EntityPlayer player)
        {
            // In case 'AssetsFinalize' method failed to add behavior to player.
            if (!player.HasBehavior<Additional.EntityBehaviorResists>()) player.SidedProperties.Behaviors.Insert(0, new Additional.EntityBehaviorResists(player));
        }
    }
}
