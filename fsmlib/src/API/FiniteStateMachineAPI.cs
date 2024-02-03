using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.API;

public interface IState
{
}
public interface IOperation : IFactoryProduct, IDisposable
{
    public enum Outcome
    {
        None,
        Started,
        Failed,
        Finished,
        StartedAndFinished
    }
    public enum Timeout
    {
        Ignore,
        Start,
        Stop
    }
    public readonly struct Result
    {
        public IState State { get; }
        public Outcome Outcome { get; }
        public Timeout Timeout { get; }
        public TimeSpan TimeoutDelay { get; }

        public Result(IState state, Outcome outcome, Timeout timeout, TimeSpan? delay = null)
        {
            State = state;
            Outcome = outcome;
            Timeout = timeout;
            TimeoutDelay = delay ?? new TimeSpan(0);
        }
    }

    Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input);

    Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input);

    struct Transition
    {
        public enum TriggerType
        {
            Input,
            Timeout
        }

        public TriggerType Trigger { get; set; }
        public string? Input { get; set; }
        public string FromState { get; set; }
        public string ToState { get; set; }

        public static Transition InputTrigger(string input, string fromState, string toState)
        {
            return new Transition() { Trigger = TriggerType.Input, Input = input, FromState = fromState, ToState = toState };
        }

        public static Transition TimeoutTrigger(string fromState, string toState)
        {
            return new Transition() { Trigger = TriggerType.Timeout, Input = null, FromState = fromState, ToState = toState };
        }

        public static implicit operator Transition((string input, string fromState, string toState) parameters)
        {
            return new Transition() { Trigger = TriggerType.Input, Input = parameters.input, FromState = parameters.fromState, ToState = parameters.toState };
        }
    }
    List<Transition> GetTransitions();

    void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems);
}
public interface ISystem : IFactoryProduct
{
    void SetSystems(Dictionary<string, ISystem> systems);
    bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters);
    bool Process(ItemSlot slot, IPlayer player, JsonObject parameters);
    string[]? GetDescription(ItemSlot slot, IWorldAccessor world);
}

public interface IBehaviourAttributesParser
{
    bool ParseDefinition(ICoreAPI api, IFactory<IOperation>? operationTypes, IFactory<ISystem>? systemTypes, IFactory<IInput>? inputTypes, JsonObject behaviourAttributes, CollectibleObject collectible);
    Dictionary<string, IOperation> GetOperations();
    Dictionary<string, ISystem> GetSystems();
    Dictionary<string, IInput> GetInputs();
}
public interface IFiniteStateMachine : IDisposable
{
    bool Process(ItemSlot slot, IPlayer player, IInput input);
    bool OnTimer(ItemSlot slot, IPlayer player, IInput input, IOperation operation);
    List<IInput> GetAvailableInputs(ItemSlot slot);
}
public interface IOperationInputInvoker
{
    bool Started(IOperation operation, ItemSlot inSlot, IPlayer player);
    bool Finished(IOperation operation, ItemSlot inSlot, IPlayer player);
}

public interface IAttributeReferencesManager : IDisposable
{
    void Substitute<TFrom>(JsonObject where, TFrom from);
}