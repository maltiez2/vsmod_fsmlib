using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

internal sealed class FiniteStateMachine : IFiniteStateMachine
{
    private sealed class State : IState
    {
        private readonly string mState;
        private readonly int mHash;

        public State(string inputState)
        {
            mState = inputState;
            mHash = mState.GetHashCode();
        }
        public override string ToString() { return mState; }
        public override bool Equals(object? obj)
        {
            return (obj as State)?.mHash == mHash;
        }
        public override int GetHashCode()
        {
            return mHash;
        }
    }

    private const string cStateAttributeName = "FSMlib.state";
    private const string cInitialStateAttribute = "initialState";

    private readonly string mInitialState;
    private readonly Dictionary<State, Dictionary<IInput, IOperation>> mOperationsByInputAndState = new();
    private readonly Dictionary<IOperation, HashSet<State>> mStatesByOperationForTimer = new();
    private readonly Dictionary<long, Utils.DelayedCallback> mTimers = new();
    private readonly List<IOperation> mOperations = new();
    private readonly IOperationInputInvoker? mOperationInputInvoker;
    private readonly CollectibleObject mCollectible;
    private readonly ICoreAPI mApi;

    private bool mDisposed = false;

    public FiniteStateMachine(ICoreAPI api, Dictionary<string, IOperation> operations, Dictionary<string, ISystem> systems, Dictionary<string, IInput> inputs, JsonObject behaviourAttributes, CollectibleObject collectible, IOperationInputInvoker? invoker)
    {
        mCollectible = collectible;
        mInitialState = behaviourAttributes[cInitialStateAttribute].AsString();
        mOperationInputInvoker = invoker;
        mApi = api;

        foreach ((string systemCode, ISystem system) in systems)
        {
            try
            {
                system.SetSystems(systems);
            }
            catch (Exception exception)
            {
                Logger.Error(api, this, $"Exception on setting systems for '{systemCode}' system for collectible '{mCollectible.Code}'.");
                Logger.Verbose(api, this, $"Exception on setting systems for '{systemCode}' system for collectible '{mCollectible.Code}'.\n\nException:\n{exception}\n");
            }

        }

        foreach ((string operationCode, IOperation operation) in operations)
        {
            try
            {
                List<IOperation.Transition> transitions = operation.GetTransitions();
                Dictionary<string, IState> operationStateMapping = ProcessTransitions(operation, transitions, inputs);
                operation.SetInputsStatesSystems(inputs, operationStateMapping, systems);
                mOperations.Add(operation);
            }
            catch (Exception exception)
            {
                Logger.Error(api, this, $"Exception on processing transitions for '{operationCode}' operation for collectible '{mCollectible.Code}'.");
                Logger.Verbose(api, this, $"Exception on processing transitions for '{operationCode}' operation for collectible '{mCollectible.Code}'.\n\nException:\n{exception}\n");
            }
        }
    }

    public List<IInput> GetAvailableInputs(ItemSlot slot)
    {
        List<IInput> inputs = new();

        State state = ReadStateFrom(slot);

        if (!mOperationsByInputAndState.ContainsKey(state))
        {
            Logger.Error(mApi, this, $"Error on getting available inputs for '{state}' state for collectible '{mCollectible.Code}': state was not found. States registered: {mOperationsByInputAndState.Count}");
            return inputs;
        }

        foreach (KeyValuePair<IInput, IOperation> item in mOperationsByInputAndState[state])
        {
            inputs.Add(item.Key);
        }

        return inputs;
    }
    public bool Process(ItemSlot slot, IPlayer player, IInput input)
    {
        if (slot?.Itemstack?.Collectible != mCollectible || player == null) return false;

        State state = ReadStateFrom(slot);
        if (!mOperationsByInputAndState.ContainsKey(state))
        {
            WriteStateTo(slot, new State(mInitialState));
            return false;
        }
        if (!mOperationsByInputAndState[state].ContainsKey(input))
        {
            return false;
        }

        IOperation operation = mOperationsByInputAndState[state][input];

        try
        {
            if (RunOperation(slot, player, operation, input, state)) return true;
        }
        catch (Exception exception)
        {
            Logger.Error(mApi, this, $"Exception on running operation '{operation}' for input '{input}' in state '{state}' for collectible '{mCollectible.Code}'.");
            Logger.Debug(mApi, this, $"Exception on running operation '{operation}' for input '{input}' in state '{state}' for collectible '{mCollectible.Code}'.\nException:\n{exception}");
        }

        return false;
    }
    public bool OnTimer(ItemSlot slot, IPlayer player, IInput input, IOperation operation)
    {
        if (slot?.Itemstack?.Collectible != mCollectible || player == null) return false;

        State state = ReadStateFrom(slot);
        if (!mStatesByOperationForTimer.ContainsKey(operation) || !mStatesByOperationForTimer[operation].Contains(state))
        {
            return false;
        }

        if (RunOperation(slot, player, operation, input, state))
        {
            return true;
        }

        return false;
    }

    private Dictionary<string, IState> ProcessTransitions(IOperation operation, List<IOperation.Transition> transitions, Dictionary<string, IInput> inputs)
    {
        Dictionary<string, IState> operationStateMapping = new();

        foreach (IOperation.Transition transition in transitions)
        {
            State initialState = new(transition.FromState);
            State finalState = new(transition.ToState);

            operationStateMapping.TryAdd(transition.FromState, initialState);
            operationStateMapping.TryAdd(transition.ToState, finalState);

            mOperationsByInputAndState.TryAdd(initialState, new());
            mOperationsByInputAndState.TryAdd(finalState, new());

            if (transition.Trigger == IOperation.Transition.TriggerType.Timeout)
            {
                mStatesByOperationForTimer.TryAdd(operation, new());
                mStatesByOperationForTimer[operation].Add(initialState);
            }
            else if (transition.Trigger == IOperation.Transition.TriggerType.Input && transition.Input != null)
            {
                mOperationsByInputAndState[initialState].TryAdd(inputs[transition.Input], operation);
            }
        }

        return operationStateMapping;
    }
    private bool RunOperation(ItemSlot slot, IPlayer player, IOperation operation, IInput input, State state)
    {
        if (player == null || slot == null) return false;

        IOperation.Outcome outcome = operation.Verify(slot, player, state, input);

        if (outcome == IOperation.Outcome.Failed)
        {
            OperationsDebugWindow.Enqueue(operation, player, input, state, state, outcome, mApi.Side == EnumAppSide.Client);
            return false;
        }

        if ((outcome == IOperation.Outcome.Started || outcome == IOperation.Outcome.StartedAndFinished) && mOperationInputInvoker?.Started(operation, slot, player) == true)
        {
            OperationsDebugWindow.Enqueue(operation, player, input, state, state, outcome, mApi.Side == EnumAppSide.Client);
            return false;
        }

        IOperation.Result result = operation.Perform(slot, player, state, input);

        OperationsDebugWindow.Enqueue(operation, player, input, state, result.State, result.Outcome, mApi.Side == EnumAppSide.Client, result);

        if (state != result.State && result.State is State resultState) WriteStateTo(slot, resultState);

        if (result.Timeout != IOperation.Timeout.Ignore && mTimers.ContainsKey(player.Entity.EntityId)) mTimers[player.Entity.EntityId]?.Cancel();

        if (result.Timeout == IOperation.Timeout.Start) mTimers[player.Entity.EntityId] = new(mApi, result.TimeoutDelay, () => OnTimer(slot, player, input, operation));

        if (result.Outcome == IOperation.Outcome.Finished) mOperationInputInvoker?.Finished(operation, slot, player);

        return input.Handle;
    }
    private State ReadStateFrom(ItemSlot slot)
    {
        State state = new(slot.Itemstack.Attributes.GetAsString(cStateAttributeName, mInitialState));
        slot.MarkDirty();
        return state;
    }
    private static void WriteStateTo(ItemSlot slot, State state)
    {
        slot.Itemstack?.Attributes?.SetString(cStateAttributeName, state.ToString());
        slot.MarkDirty();
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;

        foreach ((_, Utils.DelayedCallback timer) in mTimers)
        {
            timer.Dispose();
        }

        foreach (IOperation operation in mOperations)
        {
            operation.Dispose();
        }
    }
}
