using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IOperation;



namespace MaltiezFSM.Operations
{
    public class Instant : FactoryProduct, IOperation
    {
        private readonly Dictionary<string, string> mStatesInitialData = new();
        private readonly List<Tuple<string, JsonObject>> mSystemsInitialData = new();
        private readonly Dictionary<IState, IState> mStates = new();
        private readonly List<Tuple<ISystem, JsonObject>> mSystems = new();
        protected readonly List<Transition> mTransitions = new();
        protected readonly Dictionary<ISystem, string> mSystemsCodes = new();

        private bool mDisposed = false;

        public Instant(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
        {
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

        public virtual List<Transition> GetTransitions()
        {
            return mTransitions;
        }
        public virtual void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            foreach ((string first, string second) in mStatesInitialData)
            {
                if (!states.ContainsKey(first))
                {
                    mApi?.Logger.Warning("[FSMlib] [BasicInstant: {0}] State '{1}' not found.", mCode, first);
                    continue;
                }

                if (!states.ContainsKey(second))
                {
                    mApi?.Logger.Warning("[FSMlib] [BasicInstant: {0}] State '{1}' not found.", mCode, second);
                    continue;
                }

                mStates.Add(states[first], states[second]);
            }
            mStatesInitialData.Clear();

            foreach ((string system, JsonObject definition) in mSystemsInitialData)
            {
                if (!systems.ContainsKey(system))
                {
                    mApi?.Logger.Warning("[FSMlib] [BasicInstant: {0}] System '{1}' not found.", mCode, system);
                    continue;
                }

                mSystems.Add(new(systems[system], definition));
                mSystemsCodes.Add(systems[system], system);
            }
            mSystemsInitialData.Clear();
        }
        public virtual Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            foreach ((ISystem system, JsonObject request) in mSystems)
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
                    Logger.Error(mApi, this, $"System '{mSystemsCodes[system]}' crashed while verification in '{mCode}' operation in '{mCollectible.Code}' collectible");
                    Logger.Verbose(mApi, this, $"System '{mSystemsCodes[system]}' crashed while verification in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{request}\n\nException:{exception}");
                }
            }

            return Outcome.StartedAndFinished;
        }
        public virtual Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            foreach ((ISystem system, JsonObject request) in mSystems)
            {
                try
                {
                    system.Process(slot, player, request);
                }
                catch (Exception exception)
                {
                    Logger.Error(mApi, this, $"System '{mSystemsCodes[system]}' crashed while processing in '{mCode}' operation in '{mCollectible.Code}' collectible");
                    Logger.Verbose(mApi, this, $"System '{mSystemsCodes[system]}' crashed while processing in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{request}\n\nException:{exception}");
                }
            }

            return new(mStates[state], Outcome.StartedAndFinished, Timeout.Ignore);
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
