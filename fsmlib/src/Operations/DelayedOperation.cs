using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IOperation;

#nullable enable

namespace MaltiezFSM.Operations
{
    public class Delayed : UniqueId, IOperation
    {
        protected ICoreAPI? mApi;
        protected string? mCode;
        private bool mDisposed = false;

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
            public Outcome Outcome { get; set; }

            public static implicit operator TransitionResultInitial((string state, List<(string, JsonObject)> systemsRequests, Outcome outcome) parameters)
            {
                return new TransitionResultInitial() { state = parameters.state, systemsRequests = parameters.systemsRequests, Outcome = parameters.outcome };
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
            public IState State { get; set; }
            public List<(ISystem, JsonObject)> SystemsRequests { get; set; }
            public Outcome Outcome { get; set; }

            public static implicit operator TransitionResult((IState state, List<(ISystem, JsonObject)> systemsRequests, Outcome outcome) parameters)
            {
                return new TransitionResult() { State = parameters.state, SystemsRequests = parameters.systemsRequests, Outcome = parameters.outcome };
            }
        }
        protected struct TransitionsBranchInitial
        {
            public string initial { get; set; }
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
        protected readonly List<Transition> mTriggerConditions = new();
        protected readonly HashSet<string> mTransitionalStatesInitialData = new();

        protected readonly Dictionary<TransitionTrigger, TransitionResult> mTransitions = new();
        protected readonly Dictionary<TransitionTrigger, TimeSpan?> mTimers = new();
        protected readonly HashSet<IState> mTransitionalStates = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mApi = api;

            List<TransitionsBranchInitial> transitions = ParseTransitions(definition);
            HashSet<string> finalStates = CollectFinalStates(transitions);

            List<(string, JsonObject)> systemsInitial = ParseSystems(definition, "initial");
            List<(string, JsonObject)> systemsTimeout = ParseSystems(definition, "timeout");
            List<(string, JsonObject)> systemsContinue = ParseSystems(definition, "continue");
            Dictionary<string, List<(string, JsonObject)>> systemsFinal = new();

            List<string> inputsInitial = ParseInputs(definition, "initial");
            List<string> inputsContinue = ParseInputs(definition, "continue");
            Dictionary<string, List<string>> inputsFinal = new();

            foreach (string state in finalStates)
            {
                systemsFinal.Add(state, ParseSystems(definition, state));
                inputsFinal.Add(state, ParseInputs(definition, state));
            }

            int? timeout = definition.KeyExists("timeout") ? definition["timeout"].AsInt() : null;

            foreach (TransitionsBranchInitial transition in transitions)
            {
                mTransitionalStatesInitialData.Add(transition.intermediate);
                AddTransition(transition.initial, transition.intermediate, inputsInitial, systemsInitial, Outcome.Started);
                AddTransition(transition.intermediate, transition.intermediate, inputsContinue, systemsContinue, Outcome.None);
                AddTransitionForTimeout(timeout, transition.initial, transition.intermediate, transition.timeout, inputsInitial, systemsTimeout);

                foreach (string finalState in transition.final)
                {
                    AddTransition(transition.intermediate, finalState, inputsFinal[finalState], systemsFinal[finalState], Outcome.Finished);
                }
            }
        }
        public virtual List<Transition> GetTransitions() => mTriggerConditions;
        public virtual void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            foreach ((TransitionTriggerInitial trigger, TransitionResultInitial result) in mTransitionsInitialData)
            {
                if (!states.ContainsKey(trigger.state))
                {
                    mApi?.Logger.Warning("[FSMlib] [Delayed: {0}] State '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                if (!inputs.ContainsKey(trigger.input))
                {
                    mApi?.Logger.Warning("[FSMlib] [Delayed: {0}] Input '{1}' not found.", mCode, trigger.input);
                    continue;
                }

                List<(ISystem, JsonObject)> transitionSystems = new();
                foreach ((string system, JsonObject request) in result.systemsRequests)
                {
                    if (!systems.ContainsKey(system))
                    {
                        mApi?.Logger.Warning("[FSMlib] [Delayed: {0}] System '{1}' not found.", mCode, system);
                        continue;
                    }

                    transitionSystems.Add(new(systems[system], request));
                }

                mTransitions.Add((states[trigger.state], inputs[trigger.input]), (states[result.state], transitionSystems, result.Outcome));
            }

            foreach ((TransitionTriggerInitial trigger, int? delay) in mTimersInitialData)
            {
                if (!states.ContainsKey(trigger.state))
                {
                    mApi?.Logger.Warning("[FSMlib] [Delayed: {0}] State '{1}' not found.", mCode, trigger.state);
                    continue;
                }

                if (!inputs.ContainsKey(trigger.input))
                {
                    mApi?.Logger.Warning("[FSMlib] [Delayed: {0}] Input '{1}' not found.", mCode, trigger.input);
                    continue;
                }

                mTimers.Add((states[trigger.state], inputs[trigger.input]), delay != null ? TimeSpan.FromMilliseconds(delay.Value) : null);
            }

            mTransitionsInitialData.Clear();
            mTimersInitialData.Clear();
        }
        public virtual Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            if (!mTransitions.ContainsKey((state, input))) return Outcome.Failed;

            TransitionResult transitionResult = mTransitions[(state, input)];

            foreach ((ISystem system, JsonObject request) in transitionResult.SystemsRequests)
            {
                if (!system.Verify(slot, player, request))
                {
                    return Outcome.Failed;
                }
            }

            return transitionResult.Outcome;
        }
        public virtual Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            TransitionResult transitionResult = mTransitions[(state, input)];

            foreach ((ISystem system, JsonObject request) in transitionResult.SystemsRequests)
            {
                system.Process(slot, player, request);
            }

            Timeout timeout = transitionResult.Outcome switch
            {
                Outcome.Started => Timeout.Start,
                Outcome.Finished => mTransitionalStates.Contains(transitionResult.State) ? Timeout.Stop : Timeout.Ignore,
                _ => Timeout.Ignore,
            };
            TimeSpan? timeoutDelay = mTimers.ContainsKey((state, input)) ? mTimers[(state, input)] : null;

            return new(transitionResult.State, transitionResult.Outcome, timeout, timeoutDelay);
        }

        protected List<TransitionsBranchInitial> ParseTransitions(JsonObject definition)
        {
            List<TransitionsBranchInitial> transitions = new();

            List<JsonObject> mainTransitions = ParseField(definition, "states");
            foreach (JsonObject transition in mainTransitions)
            {
                TransitionsBranchInitial? parsed = ParseTransition(transition);

                if (parsed != null) transitions.Add(parsed.Value);
            }

            return transitions;
        }
        protected TransitionsBranchInitial? ParseTransition(JsonObject transition)
        {
            if (!transition.KeyExists("initial"))
            {
                Utils.Logger.Error(mApi, this, $"A transition from '{mCode}' operation does not contain 'initial' field");
                return null;
            }
            string initial = transition["initial"].AsString();

            if (!transition.KeyExists("timeout"))
            {
                Utils.Logger.Error(mApi, this, $"A transition from '{mCode}' operation does not contain 'timeout' field");
                return null;
            }
            string timeout = transition["timeout"].AsString();
            
            string intermediate = $"{initial}_to_{timeout}_op.{mCode}";
            if (transition.KeyExists("transitional"))
            {
                intermediate = transition["transitional"].AsString();
            }

            return new TransitionsBranchInitial(initial, intermediate, timeout, ParseFinalStates(transition));
        }
        protected static string[] ParseFinalStates(JsonObject transition)
        {
            List<JsonObject> states = ParseField(transition, "final");

            return states.Select(state => state.ToString()).ToArray();
        }
        protected static HashSet<string> CollectFinalStates(List<TransitionsBranchInitial> transitions)
        {
            HashSet<string> states = new();

            foreach (TransitionsBranchInitial transition in transitions)
            {
                foreach (string finalState in transition.final.Where(state => !states.Contains(state)))
                {
                    states.Add(finalState);
                }
            }

            return states;
        }
        protected List<(string, JsonObject)> ParseSystems(JsonObject definition, string systemsType)
        {
            List<(string, JsonObject)> systemsRequests = new();
            if (!definition.KeyExists(systemsType))
            {
                Utils.Logger.Debug(mApi, this, $"No systems in '{systemsType}' category in '{mCode}' operation");
                return systemsRequests;
            }
            foreach (JsonObject system in definition["systems"][systemsType].AsArray())
            {
                if (!system.KeyExists("code"))
                {
                    Utils.Logger.Error(mApi, this, $"A system request from '{systemsType}' category from '{mCode}' operation does not contain 'code' field");
                    continue;
                }
                
                systemsRequests.Add(new(system["code"].AsString(), system));
            }
            return systemsRequests;
        }
        protected List<string> ParseInputs(JsonObject definition, string inputsType)
        {
            if (!definition.KeyExists("inputs"))
            {
                Utils.Logger.Error(mApi, this, $"An operation '{mCode}' does not contain 'inputs' field");
                return new();
            }
            
            return ParseField(definition["inputs"], inputsType).Select((input) => input.AsString()).ToList();
        }
        protected void AddTransition(string stateFrom, string stateTo, List<string> inputs, List<(string, JsonObject)> systems, Outcome outcome)
        {
            foreach (string input in inputs)
            {
                mTransitionsInitialData.Add((stateFrom, input), (stateTo, systems, outcome));
                mTriggerConditions.Add(Transition.InputTrigger(input, stateFrom, stateTo));
            }
        }
        protected void AddTransitionForTimeout(int? timeout, string initialState, string intermediateState, string timeoutState, List<string> inputs, List<(string, JsonObject)> systems)
        {
            foreach (string inputInitial in inputs)
            {
                mTransitionsInitialData.Add((intermediateState, inputInitial), (timeoutState, systems, Outcome.Finished));
                mTimersInitialData.Add((initialState, inputInitial), timeout);
            }
            mTriggerConditions.Add(Transition.TimeoutTrigger(intermediateState, timeoutState));
        }

        protected static List<JsonObject> ParseField(JsonObject definition, string field)
        {
            List<JsonObject> transitions = new();
            if (!definition.KeyExists(field)) return transitions;

            if (definition[field].IsArray())
            {
                foreach (JsonObject transition in definition[field].AsArray())
                {
                    transitions.Add(transition);
                }
            }
            else
            {
                transitions.Add(definition[field]);
            }
            return transitions;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!mDisposed)
            {
                if (disposing)
                {
                    // Nothing to dispose
                }

                mDisposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
    }
}
