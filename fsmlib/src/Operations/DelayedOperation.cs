using HarmonyLib;
using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.Framework.FiniteStateMachine;

namespace MaltiezFSM.Operations
{
    public class Delayed : UniqueId, IOperation
    {
        public const string mainTransitionsAttrName = "states";
        public const string systemsAttrName = "systems";
        public const string inputsAttrName = "inputsToHandle";
        public const string inputsToPreventAttrName = "inputsToIntercept";
        public const string attributesAttrName = "attributes";
        public const string initialAttrName = "initial";
        public const string cancelAttrName = "cancel";
        public const string finalAttrName = "final";
        public const string delayAttrName = "delay_ms";

        protected const string cTimerInput = "";
        protected ICoreAPI mApi;
        protected string mCode;

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
        
        protected struct TransitionsBranchInitial
        {
            public string initial {  get; set; }
            public string intermediate { get; set; }
            public string timeout { get; set; }
            public string[] final { get; set; }

            public TransitionsBranchInitial(string initial, string intermediate, string timeout, params string[] final)
            {
                this.initial = initial;
                this.timeout = timeout;
                this.intermediate = intermediate;
                this.final = final;
            }

            public static implicit operator TransitionsBranchInitial((string initial, string intermediate, string timeout, string[] final) parameters)
            {
                return new TransitionsBranchInitial() { initial = parameters.initial, intermediate = parameters.intermediate, timeout = parameters.timeout, final = parameters.final };
            }
        }

        protected readonly Dictionary<TransitionTriggerInitial, TransitionResultInitial> mTransitionsInitialData = new();
        protected readonly Dictionary<TransitionTriggerInitial, int?> mTimersInitialData = new();
        protected readonly List<IOperation.Transition> mTriggerConditions = new();
        protected readonly List<string> mInputsToPreventInitialData = new();

        protected readonly Dictionary<TransitionTrigger, TransitionResult> mTransitions = new();
        protected readonly Dictionary<TransitionTrigger, int?> mTimers = new();
        protected readonly List<IInput> mInputsToPrevent = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mApi = api;

            ParseInputsToPrevent(definition);

            List<(string, JsonObject)> systemsInitial = ParseSystems(definition, initialAttrName);
            List<(string, JsonObject)> systemsCancel = ParseSystems(definition, cancelAttrName);
            List<(string, JsonObject)> systemsFinal = ParseSystems(definition, finalAttrName);

            List<string> cancelInputs = ParseInputs(definition, cancelAttrName);
            List<string> inputsInitial = ParseInputs(definition, initialAttrName);

            int? timeout = definition.KeyExists(delayAttrName) ? definition[delayAttrName].AsInt() : null;

            List<TransitionsBranchInitial> transitions = ParseTransitions(definition);
            foreach (TransitionsBranchInitial transition in transitions)
            { 
                AddTransition(transition.initial, transition.intermediate, inputsInitial, systemsInitial);
                AddTransition(transition.intermediate, transition.final[0], cancelInputs, systemsCancel);
                AddTransitionForTimeout(timeout, transition.initial, transition.intermediate, transition.timeout, inputsInitial, systemsFinal);
                AddTransitionsForInputsToPrevent(transition.intermediate);    
            }
        }

        private List<TransitionsBranchInitial> ParseTransitions(JsonObject definition)
        {
            List<TransitionsBranchInitial> transitions = new();

            JsonObject[] mainTransitions = definition[mainTransitionsAttrName].AsArray();
            foreach (JsonObject transition in mainTransitions)
            {
                string initial = transition[initialAttrName].AsString();
                string timeout = transition[finalAttrName].AsString();
                string final = transition[cancelAttrName].AsString();
                string intermediate = mCode + "_from_" + initial + "_to_" + timeout;

                transitions.Add(new TransitionsBranchInitial(initial, intermediate, timeout, final));
            }

            return transitions;
        }

        protected void ParseInputsToPrevent(JsonObject definition)
        {
            if (definition.KeyExists(inputsToPreventAttrName))
            {
                foreach (JsonObject input in definition[inputsToPreventAttrName].AsArray())
                {
                    mInputsToPreventInitialData.Add(input.AsString());
                }
            }
        }
        protected List<(string, JsonObject)> ParseSystems(JsonObject definition, string systemsType)
        {
            List<(string, JsonObject)> systemsRequests = new();
            foreach (JsonObject system in definition[systemsAttrName][systemsType].AsArray())
            {
                systemsRequests.Add(new(system["code"].AsString(), system[attributesAttrName]));
            }
            return systemsRequests;
        }
        protected List<string> ParseInputs(JsonObject definition, string inputsType)
        {
            List<string> inputs = new();
            if (definition[inputsAttrName][inputsType].IsArray())
            {
                foreach (JsonObject cancelInput in definition[inputsAttrName][inputsType].AsArray())
                {
                    inputs.Add(cancelInput.AsString());
                }
            }
            else
            {
                inputs.Add(definition[inputsAttrName][inputsType].AsString());
            }
            return inputs;
        }
        protected void AddTransition(string stateFrom, string stateTo, List<string> inputs, List<(string, JsonObject)> systems)
        {
            foreach (string input in inputs)
            {
                mTransitionsInitialData.Add((stateFrom, input), (stateTo, systems));
                mTriggerConditions.Add((input, stateFrom, stateTo));
            }
        }
        protected void AddTransitionForTimeout(int? timeout, string initialState, string intermediateState, string timeoutState, List<string> inputs, List<(string, JsonObject)> systems)
        {
            foreach (string inputInitial in inputs)
            {
                mTransitionsInitialData.Add((intermediateState, inputInitial), (timeoutState, systems));
                mTimersInitialData.Add((initialState, inputInitial), timeout);
            }
            mTriggerConditions.Add((cTimerInput, intermediateState, timeoutState));
        }
        protected void AddTransitionsForInputsToPrevent(string state)
        {
            foreach (string input in mInputsToPreventInitialData)
            {
                mTriggerConditions.Add((input, state, state));
                mTransitionsInitialData.Add((state, input), (state, new()));
            }
        }

        List<IOperation.Transition> IOperation.GetTransitions() => mTriggerConditions;
        int? IOperation.Timer(ItemSlot slot, EntityAgent player, IState state, IInput input) => mTimers.ContainsKey((state, input)) ? mTimers[(state, input)] : null;
        void IOperation.SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            foreach ((var trigger, var result) in mTransitionsInitialData)
            {
                if (!states.ContainsKey(trigger.state))
                {
                    mApi.Logger.Warning("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                if (!inputs.ContainsKey(trigger.input))
                {
                    mApi.Logger.Warning("[FSMlib] [BasicDelayed: {0}] Input '{1}' not found.", mCode, trigger.input);
                    continue;
                }

                List<(ISystem, JsonObject)> transitionSystems = new();
                foreach ((string system, JsonObject request) in result.systemsRequests)
                {
                    if (!systems.ContainsKey(system))
                    {
                        mApi.Logger.Warning("[FSMlib] [BasicDelayed: {0}] System '{1}' not found.", mCode, system);
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
                    mApi.Logger.Warning("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                if (!inputs.ContainsKey(trigger.input))
                {
                    mApi.Logger.Warning("[FSMlib] [BasicDelayed: {0}] Input '{1}' not found.", mCode, trigger.input);
                    continue;
                }

                mTimers.Add((states[trigger.state], inputs[trigger.input]), delay);
            }

            foreach (string input in mInputsToPreventInitialData)
            {
                if (!inputs.ContainsKey(input))
                {
                    mApi.Logger.Warning("[FSMlib] [BasicDelayed: {0}] Input '{1}' not found.", mCode, input);
                    continue;
                }

                mInputsToPrevent.Add(inputs[input]);
            }

            mTransitionsInitialData.Clear();
            mTimersInitialData.Clear();
            mInputsToPreventInitialData.Clear();
        }
        bool IOperation.StopTimer(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            if (!mTransitions.ContainsKey((state, input))) return false;

            return !mInputsToPrevent.Contains(input);
        }
        IState IOperation.Perform(ItemSlot slot, EntityAgent player, IState state, IInput input)
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
    }
}
