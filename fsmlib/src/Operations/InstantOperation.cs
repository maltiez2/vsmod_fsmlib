using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IOperation;

namespace MaltiezFSM.Operations;

public class Instant : BaseOperation
{
    private readonly Dictionary<string, string> mStatesInitialData = new();
    private readonly List<(string system, JsonObject request)> mSystemsInitialData = new();
    private readonly Dictionary<IState, IState> mStates = new();
    private readonly List<(ISystem system, JsonObject request)> mSystems = new();
    protected readonly List<Transition> mTransitions = new();

    public Instant(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        IEnumerable<string> inputs = ParseField(definition, "inputs").Select((input) => input.AsString());
        List<JsonObject> mainTransitions = ParseField(definition, "states");

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

    public override List<Transition> GetTransitions()
    {
        return mTransitions;
    }
    public override void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
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
            mSystemsCodes.TryAdd(systems[system], system);
        }
        mSystemsInitialData.Clear();
    }
    public override Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input)
    {
        return Verify(slot, player, mSystems) ? Outcome.StartedAndFinished : Outcome.Failed;
    }
    public override Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input)
    {
        Process(slot, player, mSystems);

        return new(mStates[state], Outcome.StartedAndFinished, Timeout.Ignore);
    }

    public override string ToString() => $"Instant: {mCode} ({mCollectible.Code})";
}
