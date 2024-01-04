using HarmonyLib;
using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



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
        private ICoreAPI? mApi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            mApi = api;

            if (mProperties == null)
            {
                Logger.Error(api, this, $"Null behavior properties on initializing FSM for: {collObj.Code}");
                return;
            }

            Logger.Debug(api, this, $"Initializing FSM for: {collObj.Code}");

            FiniteStateMachineSystem factories = api.ModLoader.GetModSystem<FiniteStateMachineSystem>();
            mInputManager = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputManager();
            IOperationInputInvoker? operationInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetOperationInputInvoker();

            IBehaviourAttributesParser parser = new TAttributesFormat();
            _ = parser.ParseDefinition(factories.GetOperationFactory(), factories.GetSystemFactory(), factories.GetInputFactory(), mProperties, collObj);

            try
            {
                mFsm = new FiniteStateMachine(api, parser.GetOperations(), parser.GetSystems(), parser.GetInputs(), mProperties, collObj, operationInputInvoker);
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
    }
}
