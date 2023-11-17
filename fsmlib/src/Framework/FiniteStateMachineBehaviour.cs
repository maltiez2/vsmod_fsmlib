using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using Vintagestory.API.Client;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace MaltiezFSM.Framework
{
    public class FiniteStateMachineBehaviour<TAttributesFormat, TFiniteStateMachine> : CollectibleBehavior, ITransformManagerProvider
        where TAttributesFormat : IBehaviourAttributesParser, new()
        where TFiniteStateMachine : IFiniteStateMachine, new()
    {
        public FiniteStateMachineBehaviour(CollectibleObject collObj) : base(collObj)
        {

        }

        private ICoreAPI mApi;
        private IFactoryProvider mFactories;
        private IFiniteStateMachine mFsm;
        private IInputManager mInputManager;
        private JsonObject mProperties;
        private readonly List<ISystem> mSystems = new();
        
        public ITransformManager transformsManager { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            mApi = api;
            mFactories = mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>();
            mInputManager = mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputManager();
            transformsManager = mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetTransformManager();

            IBehaviourAttributesParser parser = new TAttributesFormat();
            parser.ParseDefinition(mFactories.GetOperationFactory(), mFactories.GetSystemFactory(), mFactories.GetInputFactory(), mProperties, collObj);

            mFsm = new TFiniteStateMachine();
            mFsm.Init(mApi, parser.GetOperations(), parser.GetSystems(), parser.GetInputs(), mProperties, collObj);

            foreach (var inputEntry in parser.GetInputs())
            {
                mInputManager.RegisterInput(inputEntry.Value, mFsm.Process, collObj);
            }

            foreach (var systemEntry in parser.GetSystems())
            {
                mSystems.Add(systemEntry.Value);
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);

            mFactories = null;
            mFsm = null;
            mInputManager = null;
            mProperties = null;
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            mProperties = properties;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            long? entitiId = transformsManager?.GetEntityId(itemstack);
            if (entitiId != null)
            {
                ModelTransform currentTransform = transformsManager.CalcCurrentTransform((long)entitiId, target);
                renderinfo.Transform = Utils.CombineTransforms(renderinfo.Transform, currentTransform);
            }
            
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            foreach (ISystem system in mSystems)
            {
                string[] descriptions = system.GetDescription(inSlot, world);
                if (descriptions != null)
                {
                    foreach (string description in descriptions)
                    {
                        dsc.Append(description);
                    }
                }
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            List<WorldInteraction> interactionsHelp = base.GetHeldInteractionHelp(inSlot, ref handling).ToList();
            foreach (IInput input in mFsm.GetAvailableInputs(inSlot))
            {
                WorldInteraction interactionHelp = input.GetInteractionInfo(inSlot);
                if (interactionHelp != null) interactionsHelp.Add(interactionHelp);
            }

            return interactionsHelp.ToArray();
        }

        ITransformManager ITransformManagerProvider.GetTransformManager()
        {
            return transformsManager;
        }
    }
}
