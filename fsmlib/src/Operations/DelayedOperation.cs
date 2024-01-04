using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IOperation;

namespace MaltiezFSM.Operations;

public class Delayed : FactoryProduct, IOperation
{
    private bool mDisposed = false;

    protected struct TransitionTriggerInitial
    {
        public string State { get; set; }
        public string Input { get; set; }

        public static implicit operator TransitionTriggerInitial((string state, string input) parameters)
        {
            return new TransitionTriggerInitial() { State = parameters.state, Input = parameters.input };
        }
    }
    protected struct TransitionResultInitial
    {
        public string State { get; set; }
        public List<(string, JsonObject)> SystemsRequests { get; set; }
        public Outcome Outcome { get; set; }

        public static implicit operator TransitionResultInitial((string state, List<(string, JsonObject)> systemsRequests, Outcome outcome) parameters)
        {
            return new TransitionResultInitial() { State = parameters.state, SystemsRequests = parameters.systemsRequests, Outcome = parameters.outcome };
        }
    }
    protected struct TransitionTrigger
    {
        public IState State { get; set; }
        public IInput Input { get; set; }

        public static implicit operator TransitionTrigger((IState state, IInput input) parameters)
        {
            return new TransitionTrigger() { State = parameters.state, Input = parameters.input };
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
        public string Initial { get; set; }
        public string Intermediate { get; set; }
        public string Timeout { get; set; }
        public string[] Final { get; set; }

        public TransitionsBranchInitial(string initial, string intermediate, string timeout, params string[] final)
        {
            Initial = initial;
            Timeout = timeout;
            Intermediate = intermediate;
            Final = final;
        }

        public static implicit operator TransitionsBranchInitial((string initial, string intermediate, string timeout, string[] final) parameters)
        {
            return new TransitionsBranchInitial() { Initial = parameters.initial, Intermediate = parameters.intermediate, Timeout = parameters.timeout, Final = parameters.final };
        }
    }

    protected readonly Dictionary<TransitionTriggerInitial, TransitionResultInitial> mTransitionsInitialData = new();
    protected readonly Dictionary<TransitionTriggerInitial, int?> mTimersInitialData = new();
    protected readonly List<Transition> mTriggerConditions = new();
    protected readonly HashSet<string> mTransitionalStatesInitialData = new();

    protected readonly Dictionary<TransitionTrigger, TransitionResult> mTransitions = new();
    protected readonly Dictionary<TransitionTrigger, TimeSpan?> mTimers = new();
    protected readonly Dictionary<ISystem, string> mSystemsCodes = new();
    protected readonly HashSet<IState> mTransitionalStates = new();
    protected readonly StatsModifier? mDelayModifier;

    public Delayed(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        List<TransitionsBranchInitial> transitions = ParseTransitions(definition);
        HashSet<string> finalStates = CollectFinalStates(transitions);

        List<(string, JsonObject)> systemsInitial = ParseSystems(definition, "initial");
        List<(string, JsonObject)> systemsTimeout = ParseSystems(definition, "timeout");
        List<(string, JsonObject)> systemsContinue = definition.KeyExists("continue") ? ParseSystems(definition, "continue") : new();
        Dictionary<string, List<(string, JsonObject)>> systemsFinal = new();

        List<string> inputsInitial = ParseInputs(definition, "initial");
        List<string> inputsContinue = definition.KeyExists("continue") ? ParseInputs(definition, "continue") : new();
        Dictionary<string, List<string>> inputsFinal = new();

        foreach (string state in finalStates)
        {
            systemsFinal.Add(state, ParseSystems(definition, state));
            inputsFinal.Add(state, ParseInputs(definition, state));
        }

        int? timeout = definition.KeyExists("timeout") ? definition["timeout"].AsInt() : null;
        if (definition.KeyExists("timeout_stats")) mDelayModifier = new(mApi, definition["timeout_stats"].AsString());

        foreach (TransitionsBranchInitial transition in transitions)
        {
            mTransitionalStatesInitialData.Add(transition.Intermediate);
            AddTransition(transition.Initial, transition.Intermediate, inputsInitial, systemsInitial, Outcome.Started);
            AddTransition(transition.Intermediate, transition.Intermediate, inputsContinue, systemsContinue, Outcome.None);
            AddTransitionForTimeout(timeout, transition.Initial, transition.Intermediate, transition.Timeout, inputsInitial, systemsTimeout);

            foreach (string finalState in transition.Final)
            {
                AddTransition(transition.Intermediate, finalState, inputsFinal[finalState], systemsFinal[finalState], Outcome.Finished);
            }
        }
    }
    public virtual List<Transition> GetTransitions() => mTriggerConditions;
    public virtual void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
    {
        foreach ((TransitionTriggerInitial trigger, TransitionResultInitial result) in mTransitionsInitialData)
        {
            if (!states.ContainsKey(trigger.State))
            {
                LogWarn($"State '{trigger.State}' not found");
                continue;
            }

            if (!inputs.ContainsKey(trigger.Input))
            {
                LogWarn($"Input '{trigger.Input}' not found");
                continue;
            }

            List<(ISystem, JsonObject)> transitionSystems = new();
            foreach ((string system, JsonObject request) in result.SystemsRequests)
            {
                if (!systems.ContainsKey(system))
                {
                    LogWarn($"System '{system}' not found");
                    continue;
                }

                transitionSystems.Add(new(systems[system], request));
                mSystemsCodes.TryAdd(systems[system], system);
            }

            mTransitions.Add((states[trigger.State], inputs[trigger.Input]), (states[result.State], transitionSystems, result.Outcome));
        }

        foreach ((TransitionTriggerInitial trigger, int? delay) in mTimersInitialData)
        {
            if (!states.ContainsKey(trigger.State))
            {
                LogWarn($"State '{trigger.State}' not found");
                continue;
            }

            if (!inputs.ContainsKey(trigger.Input))
            {
                LogWarn($"Input '{trigger.Input}' not found");
                continue;
            }

            mTimers.Add((states[trigger.State], inputs[trigger.Input]), delay != null ? TimeSpan.FromMilliseconds(delay.Value) : null);
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
            try
            {
                if (!system.Verify(slot, player, request))
                {
                    return Outcome.Failed;
                }
            }
            catch (Exception exception)
            {
                LogError($"System '{mSystemsCodes[system]}' crashed while verification in '{mCode}' operation in '{mCollectible.Code}' collectible");
                LogVerbose($"System '{mSystemsCodes[system]}' crashed while verification in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{request}\n\nException:\n{exception}\n");
            }
        }

        return transitionResult.Outcome;
    }
    public virtual Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input)
    {
        TransitionResult transitionResult = mTransitions[(state, input)];

        foreach ((ISystem system, JsonObject request) in transitionResult.SystemsRequests)
        {
            try
            {
                system.Process(slot, player, request);
            }
            catch (Exception exception)
            {
                LogError($"System '{mSystemsCodes[system]}' crashed while processing in '{mCode}' operation in '{mCollectible.Code}' collectible.");
                LogVerbose($"System '{mSystemsCodes[system]}' crashed while processing in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{request}\n\nException:\n{exception}\n");
            }
        }

        Timeout timeout = transitionResult.Outcome switch
        {
            Outcome.Started => Timeout.Start,
            Outcome.Finished => mTransitionalStates.Contains(transitionResult.State) ? Timeout.Stop : Timeout.Ignore,
            _ => Timeout.Ignore,
        };

        return new(transitionResult.State, transitionResult.Outcome, timeout, GetTimeout(player, state, input));
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
            LogError($"A transition from '{mCode}' operation does not contain 'initial' field");
            return null;
        }
        string initial = transition["initial"].AsString();

        if (!transition.KeyExists("timeout"))
        {
            LogError($"A transition from '{mCode}' operation does not contain 'timeout' field");
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
            foreach (string finalState in transition.Final.Where(state => !states.Contains(state)))
            {
                states.Add(finalState);
            }
        }

        return states;
    }
    protected List<(string, JsonObject)> ParseSystems(JsonObject definition, string systemsType)
    {
        List<(string, JsonObject)> systemsRequests = new();
        if (!definition.KeyExists("systems") || !definition["systems"].KeyExists(systemsType))
        {
            LogDebug($"No systems in '{systemsType}' category in '{mCode}' operation");
            return systemsRequests;
        }
        foreach (JsonObject system in definition["systems"][systemsType].AsArray())
        {
            if (!system.KeyExists("code"))
            {
                LogError($"A system request from '{systemsType}' category from '{mCode}' operation does not contain 'code' field");
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
            LogError($"An operation '{mCode}' does not contain 'inputs' field");
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
    protected TimeSpan? GetTimeout(IPlayer player, IState state, IInput input)
    {
        TimeSpan? timeoutDelay = mTimers.ContainsKey((state, input)) ? mTimers[(state, input)] : null;

        if (timeoutDelay != null && mDelayModifier != null)
        {
            timeoutDelay = mDelayModifier.CalcMilliseconds(player, timeoutDelay.Value);
        }

        return timeoutDelay;
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
        GC.SuppressFinalize(this);
    }
}
