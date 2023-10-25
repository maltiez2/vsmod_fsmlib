using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using Vintagestory.API.Client;
using System.Text;
using Vintagestory.API.Config;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common.Entities;
using System.Reflection.Emit;

namespace MaltiezFSM.Framework
{
    public class FiniteStateMachineBehaviour : CollectibleBehavior
    {
        public FiniteStateMachineBehaviour(CollectibleObject collObj) : base(collObj)
        {

        }

        private ICoreAPI mApi;
        private IFactoryProvider mFactories;
        private IFiniteStateMachine mFsm;
        private IInputManager mInputIterceptor;
        private JsonObject mProperties;
        private readonly List<ISystem> mSystems = new();
        
        public TransformsManager transformsManager { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            mApi = api;
            mFactories = mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>();
            mInputIterceptor = mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputInterceptor();
            transformsManager = new(api);

            IBehaviourAttributesParser parser = new BehaviourAttributesParser();
            parser.ParseDefinition(mFactories.GetOperationFactory(), mFactories.GetSystemFactory(), mFactories.GetInputFactory(), mProperties, collObj);

            mFsm = new FiniteStateMachine();
            mFsm.Init(mApi, parser.GetOperations(), parser.GetSystems(), parser.GetInputs(), mProperties, collObj);

            foreach (var inputEntry in parser.GetInputs())
            {
                mInputIterceptor.RegisterInput(inputEntry.Value, mFsm.Process, collObj);
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
            mInputIterceptor = null;
            mProperties = null;
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            mProperties = properties;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            transformsManager.CalcCurrentTransform(capi.World.Player.Entity.EntityId, target);
            renderinfo.Transform = Utils.CombineTransforms(renderinfo.Transform, transformsManager.currentTransform);

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
    }
}
