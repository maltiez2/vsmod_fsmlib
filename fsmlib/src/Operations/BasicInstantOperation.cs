using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using System;
using Vintagestory.API.Common.Entities;

namespace MaltiezFSM.Operations
{
    public class BasicInstant : UniqueIdFactoryObject, IOperation
    {
        public const string mainTransitionsAttrName = "states";
        public const string systemsAttrName = "systems";
        public const string initialStateAttrName = "initial";
        public const string finalStateAttrName = "final";
        public const string inputAttrName = "input";
        public const string attributesAttrName = "attributes";
        public const string inputsToInterceptAttrName = "inputsToIntercept";

        private readonly Dictionary<string, string> mStatesInitialData = new();
        private readonly List<Tuple<string, JsonObject>> mSystemsInitialData = new();
        private readonly List<Tuple<string, string, string>> mTransitions = new();
        private readonly List<string> mInputsToPreventInitialData = new();

        private readonly Dictionary<IState, IState> mStates = new();
        private readonly List<Tuple<ISystem, JsonObject>> mSystems = new();
        private readonly List<IInput> mInputsToPrevent = new();

        private string mCode;
        private ICoreAPI mApi;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mApi = api;

            List<string> inputs = new List<string>();
            if (definition[inputAttrName].IsArray())
            {
                foreach (JsonObject input in definition[inputAttrName].AsArray())
                {
                    inputs.Add(input.AsString());
                }
            }
            else
            {
                inputs.Add(definition[inputAttrName].AsString());
            }

            if (definition.KeyExists(inputsToInterceptAttrName))
            {
                foreach (JsonObject input in definition[inputsToInterceptAttrName].AsArray())
                {
                    mInputsToPreventInitialData.Add(input.AsString());
                }
            }

            JsonObject[] mainTransitions = definition[mainTransitionsAttrName].AsArray();
            foreach (JsonObject transition in mainTransitions)
            {
                mStatesInitialData.Add(transition[initialStateAttrName].AsString(), transition[finalStateAttrName].AsString());

                foreach (string input in inputs)
                {
                    mTransitions.Add(new(input, transition[initialStateAttrName].AsString(), transition[finalStateAttrName].AsString()));
                }

                foreach (string input in mInputsToPreventInitialData)
                {
                    mTransitions.Add(new(input, transition[initialStateAttrName].AsString(), transition[initialStateAttrName].AsString()));
                }
            }

            JsonObject[] systems = definition[systemsAttrName].AsArray();
            foreach (JsonObject system in systems)
            {
                mSystemsInitialData.Add(new (system["code"].AsString(), system));
            }
        }

        public List<Tuple<string, string, string>> GetTransitions()
        {
            return mTransitions;
        }

        public void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            foreach (var entry in mStatesInitialData)
            {
                if (!states.ContainsKey(entry.Key))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, entry.Key);
                    continue;
                }

                if (!states.ContainsKey(entry.Value))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, entry.Value);
                    continue;
                }

                mStates.Add(states[entry.Key], states[entry.Value]);
            }
            mStatesInitialData.Clear();

            foreach (var entry in mSystemsInitialData)
            {
                if (!systems.ContainsKey(entry.Item1))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, entry.Item1);
                    continue;
                }

                mSystems.Add(new(systems[entry.Item1], entry.Item2));
            }
            mSystemsInitialData.Clear();

            foreach (string input in mInputsToPreventInitialData)
            {
                if (!systems.ContainsKey(input))
                {
                    mApi.Logger.Debug("[FSMlib] [BasicDelayed: {0}] State '{1}' not found.", mCode, input);
                    continue;
                }

                mInputsToPrevent.Add(inputs[input]);
            }
            mInputsToPreventInitialData.Clear();
        }

        public IState Perform(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            if (mInputsToPrevent.Contains(input)) return state;
            
            foreach (var entry in mSystems)
            {
                if (!entry.Item1.Verify(slot, player, entry.Item2[attributesAttrName]))
                {
                    return state;
                }
            }

            foreach (var entry in mSystems)
            {
                entry.Item1.Process(slot, player, entry.Item2[attributesAttrName]);
            }

            return mStates[state];
        }
        public bool StopTimer(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            return false;
        }
        public int? Timer(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            return null;
        }
    }
}
