﻿using Vintagestory.API.Common;
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

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("NoMelee", typeof(NoMelee));
            api.RegisterCollectibleBehaviorClass("FiniteStateMachine", typeof(Framework.FiniteStateMachineBehaviour));

            api.RegisterEntityBehaviorClass("constresist", typeof(Additional.EntityBehaviorConstResists<Additional.ConstResistance>));
            api.RegisterEntityBehaviorClass("tempresists", typeof(Additional.EntityBehaviorResists));

            if (api.Side == EnumAppSide.Server) (api as ICoreServerAPI).Event.PlayerJoin += (byPlayer) => AddPlayerBehavior(byPlayer.Entity);

            mOperationFactory = new Framework.Factory<IOperation, Framework.UniqueIdGeneratorForFactory>(api);
            mSystemFactory = new Framework.Factory<ISystem, Framework.UniqueIdGeneratorForFactory>(api);
            mInputFactory = new Framework.Factory<IInput, Framework.UniqueIdGeneratorForFactory>(api)

            RegisterSystems();
            RegisterOperations();
            RegisterInputs();

            IActiveSlotListener activeSlotListener = (api.Side == EnumAppSide.Client) ? new Framework.ActiveSlotActiveListener(api as ICoreClientAPI) : null;
            IHotkeyInputManager hotkeyInputManager = (api.Side == EnumAppSide.Client) ? new Framework.HotkeyInputManager(api as ICoreClientAPI) : null;
            IStatusInputManager statusInputManager = (api.Side == EnumAppSide.Client) ? new Framework.StatusInputManager(api as ICoreClientAPI) : null;
            IKeyInputManager    keyInputManager    = (api.Side == EnumAppSide.Client) ? new Framework.KeyInputManager(api as ICoreClientAPI) : null;
            mInputManager = new Framework.InputManager(api, activeSlotListener, hotkeyInputManager, statusInputManager, keyInputManager);
        }

        public void RegisterSystems()
        {  
            mSystemFactory.RegisterType<Systems.BasicSoundSystem>("Sound");
            mSystemFactory.RegisterType<Systems.BasicReload>("Reload");
            mSystemFactory.RegisterType<Systems.BasicShooting>("Shooting");
            mSystemFactory.RegisterType<Systems.BasicVariantsAnimation<Systems.TickBasedAnimation>>("VariantsAnimation");
            mSystemFactory.RegisterType<Systems.BasicRequirements>("Requirements");
            mSystemFactory.RegisterType<Systems.BasicTransformAnimation>("TransformAnimation");
            mSystemFactory.RegisterType<Systems.BasicPlayerAnimation>("PlayerAnimation");
            mSystemFactory.RegisterType<Systems.BasicPlayerStats>("PlayerStats");
            mSystemFactory.RegisterType<Systems.BasicParticles>("Particles");
            mSystemFactory.RegisterType<Systems.BasicAim>("Aiming");
            mSystemFactory.RegisterType<Systems.NoSway>("NoSway");
            mSystemFactory.RegisterType<Systems.NoSprint>("NoSprint");
            mSystemFactory.RegisterType<Systems.ChangeGroup>("ChangeGroup");
            mSystemFactory.RegisterType<Systems.BasicMelee>("BasicMelee");
            mSystemFactory.RegisterType<Systems.BasicDurabilityDamage>("DurabilityDamage");
            mSystemFactory.RegisterType<Systems.BasicDurability>("Durability");
            mSystemFactory.RegisterType<Systems.ItemStackGiver>("ItemStackGiver");
            mSystemFactory.RegisterType<Systems.SimpleMelee>("SimpleMelee");
            mSystemFactory.RegisterType<Systems.BasicParry<Additional.EntityBehaviorResists>>("BasicParry");
        }
        public void RegisterOperations()
        {
            mOperationFactory.RegisterType<Operations.BasicInstant>("Instant");
            mOperationFactory.RegisterType<Operations.BasicDelayed>("Delayed");
        }

        public void RegisterInputs()
        {
            mInputFactory.RegisterType<Inputs.BasicKey>("Key");
            mInputFactory.RegisterType<Inputs.BasicMouse>("MouseKey");
            mInputFactory.RegisterType<Inputs.BasicHotkey>("Hotkey");
            mInputFactory.RegisterType<Inputs.SlotModified>("SlotModified");
            mInputFactory.RegisterType<Inputs.BasicSlotBefore>("SlotChange");
            mInputFactory.RegisterType<Inputs.BasicSlotAfter>("SlotSelected");
            mInputFactory.RegisterType<Inputs.ItemDropped>("ItemDropped");
            mInputFactory.RegisterType<Inputs.Swimming>("Swimming");
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
        public IInputManager GetInputInterceptor()
        {
            return mInputManager;
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
