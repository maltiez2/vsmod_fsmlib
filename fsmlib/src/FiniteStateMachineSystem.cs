﻿using Vintagestory.API.Common;
using MaltiezFirearms.FiniteStateMachine.API;
using Vintagestory.API.Client;

namespace MaltiezFirearms.FiniteStateMachine
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

            api.RegisterCollectibleBehaviorClass("FirearmsFiniteStateMachine", typeof(Framework.FiniteStateMachineBehaviour));

            mOperationFactory = new Framework.Factory<IOperation, Framework.UniqueIdGeneratorForFactory>(api);
            mSystemFactory = new Framework.Factory<ISystem, Framework.UniqueIdGeneratorForFactory>(api);
            mInputFactory = new Framework.Factory<IInput, Framework.UniqueIdGeneratorForFactory>(api);

            RegisterSystems();
            RegisterOperations();
            RegisterInputs();

            IActiveSlotListener activeSlotListener = (api.Side == EnumAppSide.Client) ? new Framework.ActiveSlotActiveListener(api as ICoreClientAPI) : null;
            IHotkeyInputManager hotkeyInputManager = (api.Side == EnumAppSide.Client) ? new Framework.HotkeyInputManager(api as ICoreClientAPI) : null;
            mInputManager = new Framework.InputManager(api, activeSlotListener, hotkeyInputManager);
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
            mSystemFactory.RegisterType<Systems.ChangeGroup>("ChangeGroup");
            mSystemFactory.RegisterType<Systems.BasicMelee>("BasicMelee");
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
            mInputFactory.RegisterType<Inputs.BasicSlotBefore>("SlotChange");
            mInputFactory.RegisterType<Inputs.ItemDropped>("ItemDropped");
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
    }
}
