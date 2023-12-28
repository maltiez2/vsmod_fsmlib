using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable enable

namespace MaltiezFSM.Framework
{
    public class FiniteStateMachineBehaviour<TAttributesFormat> : CollectibleBehavior
        where TAttributesFormat : IBehaviourAttributesParser, new()
    {
        public FiniteStateMachineBehaviour(CollectibleObject collObj) : base(collObj)
        {

        }

        private IFiniteStateMachine? mFsm;
        private IInputManager? mInputManager;
        private JsonObject? mProperties;
        private readonly List<ISystem> mSystems = new();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Utils.Logger.Debug(api, this, $"Started FSM for: {collObj.Code}");

            FiniteStateMachineSystem factories = api.ModLoader.GetModSystem<FiniteStateMachineSystem>();
            mInputManager = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputManager();
            IOperationInputInvoker? operationInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetOperationInputInvoker();

            IBehaviourAttributesParser parser = new TAttributesFormat();
            parser.ParseDefinition(factories.GetOperationFactory(), factories.GetSystemFactory(), factories.GetInputFactory(), mProperties, collObj);

            mFsm = new FiniteStateMachine(api, parser.GetOperations(), parser.GetSystems(), parser.GetInputs(), mProperties, collObj, operationInputInvoker);

            var operations = parser.GetOperations();

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
                        Utils.Logger.Warn(api, this, $"Operation '{operationInput.OperationCode}' from input '{input}' is not found");
                        continue;
                    }
                }

                mInputManager.RegisterInput(input, mFsm.Process, collObj);
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

            if (mFsm == null) return interactionsHelp.ToArray();

            foreach (IInput input in mFsm.GetAvailableInputs(inSlot))
            {
                WorldInteraction? interactionHelp = input.GetInteractionInfo(inSlot);
                if (interactionHelp != null) interactionsHelp.Add(interactionHelp);
            }

            return interactionsHelp.ToArray();
        }
    }
}
