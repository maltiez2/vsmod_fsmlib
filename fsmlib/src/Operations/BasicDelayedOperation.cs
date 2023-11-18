using HarmonyLib;
using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Operations
{
    public interface ITransition
    {
        void Init(IState initial, IState final, Dictionary<ISystem, JsonObject> systems);

    }
    
    public class BasicDelayed : UniqueId, IOperation
    {
        public const string mainTransitionsAttrName = "states";
        public const string systemsAttrName = "systems";
        public const string inputsAttrName = "inputsToHandle";
        public const string inputsToInterceptAttrName = "inputsToIntercept";
        public const string attributesAttrName = "attributes";
        public const string initialName = "initial";
        public const string cancelAttrName = "cancel";
        public const string finalAttrName = "final";
        public const string delayAttrName = "delay_ms";

        protected const string cTimerInput = "";

        protected ICoreAPI mApi;
        private string mCode;

        protected struct TransitionTriggerInitial
        {
            public string state { get; set; }
            public string input { get; set; }

            public static implicit operator TransitionTriggerInitial((string state, string input) parameters)
            {
                return new TransitionTriggerInitial() { state = parameters.state, input = parameters.input };
            }
        }
        protected struct TransitionResultInitial
        {
            public string state { get; set; }
            public List<(string, JsonObject)> systemsRequests { get; set; }

            public static implicit operator TransitionResultInitial((string state, List<(string, JsonObject)> systemsRequests) parameters)
            {
                return new TransitionResultInitial() { state = parameters.state, systemsRequests = parameters.systemsRequests };
            }
        }

        protected struct TransitionTrigger
        {
            public IState state { get; set; }
            public IInput input { get; set; }

            public static implicit operator TransitionTrigger((IState state, IInput input) parameters)
            {
                return new TransitionTrigger() { state = parameters.state, input = parameters.input };
            }
        }
        protected struct TransitionResult
        {
            public IState state { get; set; }
            public List<(ISystem, JsonObject)> systemsRequests { get; set; }

            public static implicit operator TransitionResult((IState state, List<(ISystem, JsonObject)> systemsRequests) parameters)
            {
                return new TransitionResult() { state = parameters.state, systemsRequests = parameters.systemsRequests };
            }
        }
        

        // Initial data for operation's logic
        protected readonly Dictionary<TransitionTriggerInitial, TransitionResultInitial> mTransitionsInitialData = new();
        protected readonly Dictionary<TransitionTriggerInitial, int?> mTimersInitialData = new();
        protected readonly List<IOperation.Transition> mTriggerConditions = new();
        protected readonly List<(string, string)> mStatesInitialData = new();
        protected readonly List<string> mInputsInitialData = new();
        protected readonly List<string> mInputsToPreventInitialData = new();

        // Final data for operation's logic
        protected readonly Dictionary<TransitionTrigger, TransitionResult> mTransitions = new();
        protected readonly Dictionary<TransitionTrigger, int?> mTimers = new();
        protected readonly List<IInput> mInputsToPrevent = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;

            if (definition.KeyExists(inputsToInterceptAttrName))
            {
                foreach (JsonObject input in definition[inputsToInterceptAttrName].AsArray())
                {
                    mInputsToPreventInitialData.Add(input.AsString());
                }
            }

            mApi = api;

            List<(string, JsonObject)> systemsInitial = new();
            List<(string, JsonObject)> systemsCancel = new();
            List<(string, JsonObject)> systemsFinal = new();

            JsonObject[] systems = definition[systemsAttrName][initialName].AsArray();
            foreach (JsonObject system in systems)
            {
                systemsInitial.Add(new (system["code"].AsString(), system[attributesAttrName]));
            }
            systems = definition[systemsAttrName][cancelAttrName].AsArray();
            foreach (JsonObject system in systems)
            {
                systemsCancel.Add(new(system["code"].AsString(), system[attributesAttrName]));
            }
            systems = definition[systemsAttrName][finalAttrName].AsArray();
            foreach (JsonObject system in systems)
            {
                systemsFinal.Add(new(system["code"].AsString(), system[attributesAttrName]));
            }

            List<string> cancelInputs = new();
            if (definition[inputsAttrName][cancelAttrName].IsArray())
            {
                foreach (JsonObject cancelInput in definition[inputsAttrName][cancelAttrName].AsArray())
                {
                    cancelInputs.Add(cancelInput.AsString());
                    mInputsInitialData.Add(cancelInput.AsString());
                }
            }
            else
            {
                cancelInputs.Add(definition[inputsAttrName][cancelAttrName].AsString());
                mInputsInitialData.Add(definition[inputsAttrName][cancelAttrName].AsString());
            }

            List<string> inputsInitial = new List<string>();
            if (definition[inputsAttrName][initialName].IsArray())
            {
                foreach (JsonObject input in definition[inputsAttrName][initialName].AsArray())
                {
                    inputsInitial.Add(input.AsString());
                }
            }
            else
            {
                inputsInitial.Add(definition[initialName].AsString());
            }

            int? timerDelay = definition.KeyExists(delayAttrName) ? definition[delayAttrName].AsInt() : null;

            JsonObject[] mainTransitions = definition[mainTransitionsAttrName].AsArray();
            foreach (JsonObject transition in mainTransitions)
            {
                string initialState = transition[initialName].AsString();
                string finalState = transition[finalAttrName].AsString();
                string cancelState = transition[cancelAttrName].AsString();
                string intermediateState = initialState + "_to_" + finalState + "_op." + code;

                mStatesInitialData.Add((initialState, intermediateState));
                mStatesInitialData.Add((intermediateState, finalState));
                mStatesInitialData.Add((intermediateState, cancelState));

                foreach (string inputInitial in inputsInitial)  mTransitionsInitialData.Add((initialState, inputInitial), (intermediateState, systemsInitial));
                foreach (string inputInitial in inputsInitial) mTransitionsInitialData.Add((intermediateState, inputInitial), (finalState, systemsFinal));
                foreach (string inputCancel in cancelInputs) mTransitionsInitialData.Add((intermediateState, inputCancel), (inputCancel, systemsCancel));

                foreach (string inputInitial in inputsInitial) mTimersInitialData.Add((initialState, inputInitial), timerDelay);

                foreach (string inputInitial in inputsInitial) mTriggerConditions.Add((initialState, inputInitial, intermediateState));
                foreach (string inputCancel in cancelInputs) mTriggerConditions.Add((intermediateState, inputCancel, cancelState));
                mTriggerConditions.Add((intermediateState, cTimerInput, finalState));

                foreach (string input in mInputsToPreventInitialData)
                {
                    mTriggerConditions.Add((intermediateState, input, intermediateState));
                    mStatesInitialData.Add((intermediateState, intermediateState));
                    mTransitionsInitialData.Add((intermediateState, input), (intermediateState, new()));
                }
            }

        }

        public List<IOperation.Transition> GetTransitions()
        {
            return mTriggerConditions;
        }

        public void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            foreach ((var trigger, var result) in mTransitionsInitialData)
            {
                if (!states.ContainsKey(trigger.state))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                if (!inputs.ContainsKey(trigger.input))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] Input '{1}' not found.", mCode, trigger.input);
                    continue;
                }

                List<(ISystem, JsonObject)> transitionSystems = new();
                foreach ((string system, JsonObject request) in result.systemsRequests)
                {
                    if (!systems.ContainsKey(system))
                    {
                        mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] System '{1}' not found.", mCode, system);
                        continue;
                    }

                    transitionSystems.Add(new (systems[system], request));
                }

                mTransitions.Add((states[trigger.state], inputs[trigger.input]), (states[result.state], transitionSystems));
            }

            foreach ((var trigger, int? delay) in mTimersInitialData)
            {
                if (!states.ContainsKey(trigger.state))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                if (!inputs.ContainsKey(trigger.state))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] Input '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                mTimers.Add((states[trigger.state], inputs[trigger.input]), delay);
            }

            foreach (string input in mInputsToPreventInitialData)
            {
                if (!inputs.ContainsKey(input))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] Input '{1}' not found.", mCode, input);
                    continue;
                }

                mInputsToPrevent.Add(inputs[input]);
            }

            mStatesInitialData.Clear();
            mTransitionsInitialData.Clear();
            mTimersInitialData.Clear();
            mInputsInitialData.Clear();
            mInputsToPreventInitialData.Clear();
        }
        public bool StopTimer(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            if (!mTransitions.ContainsKey((state, input))) return false;

            return !mInputsToPrevent.Contains(input);
        }
        public IState Perform(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            if (!mTransitions.ContainsKey((state, input))) return state;
            
            TransitionResult transitionResult = mTransitions[(state, input)];

            foreach (var entry in transitionResult.systemsRequests)
            {
                if (!entry.Item1.Verify(slot, player, entry.Item2))
                {
                    return state;
                }
            }

            foreach (var entry in transitionResult.systemsRequests)
            {
                entry.Item1.Process(slot, player, entry.Item2);
            }

            return transitionResult.state;
        }
        public int? Timer(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            return mTimers.ContainsKey((state, input)) ? mTimers[(state, input)] : null;
        }
    }
}
