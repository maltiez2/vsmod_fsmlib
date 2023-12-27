using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable enable

namespace MaltiezFSM.Operations
{
    public class Instant : UniqueId, IOperation
    {
        private readonly Dictionary<string, string> mStatesInitialData = new();
        private readonly List<Tuple<string, JsonObject>> mSystemsInitialData = new();
        private readonly Dictionary<IState, IState> mStates = new();
        private readonly List<Tuple<ISystem, JsonObject>> mSystems = new();

        protected readonly List<IOperation.Transition> mTransitions = new();
        protected ICoreAPI? mApi;

        private string? mCode;
        private bool mDisposed = false;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mApi = api;

            IEnumerable<string> inputs = ParseField(definition, "inputs").Select((input) => input.AsString());
            List<JsonObject> mainTransitions = ParseField(definition, "transitions");
            
            foreach (JsonObject transition in mainTransitions)
            {
                mStatesInitialData.Add(transition["initial"].AsString(), transition["final"].AsString());

                foreach (string input in inputs)
                {
                    mTransitions.Add((input, transition["initial"].AsString(), transition["final"].AsString()));
                }
            }

            List<JsonObject> systems = ParseField(definition, "systems");
            foreach (JsonObject system in systems)
            {
                mSystemsInitialData.Add(new(system["code"].AsString(), system));
            }
        }

        private static List<JsonObject> ParseField(JsonObject definition, string field)
        {
            List<JsonObject> transitions = new();
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

        public virtual List<IOperation.Transition> GetTransitions()
        {
            return mTransitions;
        }
        public virtual void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            foreach (KeyValuePair<string, string> entry in mStatesInitialData)
            {
                if (!states.ContainsKey(entry.Key))
                {
                    mApi?.Logger.Warning("[FSMlib] [BasicInstant: {0}] State '{1}' not found.", mCode, entry.Key);
                    continue;
                }

                if (!states.ContainsKey(entry.Value))
                {
                    mApi?.Logger.Warning("[FSMlib] [BasicInstant: {0}] State '{1}' not found.", mCode, entry.Value);
                    continue;
                }

                mStates.Add(states[entry.Key], states[entry.Value]);
            }
            mStatesInitialData.Clear();

            foreach (Tuple<string, JsonObject> entry in mSystemsInitialData)
            {
                if (!systems.ContainsKey(entry.Item1))
                {
                    mApi?.Logger.Warning("[FSMlib] [BasicInstant: {0}] System '{1}' not found.", mCode, entry.Item1);
                    continue;
                }

                mSystems.Add(new(systems[entry.Item1], entry.Item2));
            }
            mSystemsInitialData.Clear();
        }
        public virtual IOperation.Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            foreach (Tuple<ISystem, JsonObject> entry in mSystems)
            {
                if (!entry.Item1.Verify(slot, player, entry.Item2["attributes"]))
                {
                    return IOperation.Outcome.Failed;
                }
            }

            return IOperation.Outcome.StartedAndFinished;
        }
        public virtual IOperation.Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            foreach (Tuple<ISystem, JsonObject> entry in mSystems)
            {
                entry.Item1.Process(slot, player, entry.Item2["attributes"]);
            }

            return new(mStates[state], IOperation.Outcome.StartedAndFinished, IOperation.Timeout.Ignore);
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
}
