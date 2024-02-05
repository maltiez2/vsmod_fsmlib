using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using VSImGui;

namespace MaltiezFSM.Framework;

internal sealed class FiniteStateMachine : IFiniteStateMachine
{
    private const string cInitialStateAttribute = "initialState";

    private readonly Dictionary<IState, Dictionary<IInput, IOperation>> mOperationsByInputAndState = new();
    private readonly Dictionary<IOperation, HashSet<IState>> mStatesByOperationForTimer = new();
    private readonly Dictionary<long, Utils.DelayedCallback> mTimers = new();
    private readonly List<IOperation> mOperations = new();
    private readonly IOperationInputInvoker? mOperationInputInvoker;
    private readonly CollectibleObject mCollectible;
    private readonly StateManager mStateManager;
    private readonly ICoreAPI mApi;

    private bool mDisposed = false;

    public FiniteStateMachine(ICoreAPI api, Dictionary<string, IOperation> operations, Dictionary<string, ISystem> systems, Dictionary<string, IInput> inputs, JsonObject behaviourAttributes, CollectibleObject collectible, IOperationInputInvoker? invoker)
    {
        mCollectible = collectible;
        string initialState = behaviourAttributes[cInitialStateAttribute].AsString();
        mOperationInputInvoker = invoker;
        mStateManager = new(api, initialState);
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

        Logger.Debug(api, this, $"Initialized for '{collectible.Code}'. States: {mOperationsByInputAndState.Count}, operations: {mOperations.Count}");
    }

    public List<IInput> GetAvailableInputs(ItemSlot slot)
    {
        List<IInput> inputs = new();

        IState state = mStateManager.Get(slot);

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

        IState state = mStateManager.Get(slot);
        if (!mOperationsByInputAndState.ContainsKey(state))
        {
            mStateManager.Reset(slot);
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

        IState state = mStateManager.Get(slot);
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
            IState initialState = StateManager.Construct(transition.FromState);
            IState finalState = StateManager.Construct(transition.ToState);

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
    private bool RunOperation(ItemSlot slot, IPlayer player, IOperation operation, IInput input, IState state)
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

        mStateManager.Set(slot, result.State);

        if (result.Timeout != IOperation.Timeout.Ignore && mTimers.ContainsKey(player.Entity.EntityId)) mTimers[player.Entity.EntityId]?.Cancel();

        if (result.Timeout == IOperation.Timeout.Start) mTimers[player.Entity.EntityId] = new(mApi, result.TimeoutDelay, () => OnTimer(slot, player, input, operation));

        if (result.Outcome == IOperation.Outcome.Finished) mOperationInputInvoker?.Finished(operation, slot, player);

        return input.Handle;
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

internal sealed class StateManager
{
    private const string cStateAttributeNameClient = "FSMlib.state.client";
    private const string cStateAttributeNameServer = "FSMlib.state.server";
    private const string cSyncAttributeName = "FSMlib.sync";
#if DEBUG
    private TimeSpan mSynchronizationDelay = TimeSpan.FromMilliseconds(90); // 3+ game ticks (+1 from HoldButtonManager delay)
#else
    private readonly TimeSpan mSynchronizationDelay = TimeSpan.FromMilliseconds(90);
#endif
    private readonly string mInitialState;
    private readonly ICoreAPI mApi;

    public StateManager(ICoreAPI api, string initialState)
    {
        mApi = api;
        mInitialState = initialState;

#if DEBUG
        DebugWindow.IntSlider("fsmlib", "tweaks", "state sync delay", 0, 1000, () => (int)mSynchronizationDelay.TotalMilliseconds, value => mSynchronizationDelay = TimeSpan.FromMilliseconds(value));
#endif
    }
    public IState Get(ItemSlot slot) => ReadStateFrom(slot);
    public void Set(ItemSlot slot, IState state)
    {
        if (state is not State supportedState)
        {
            Logger.Error(mApi, this, $"Unsupported state class was passed: {state}");
            return;
        }
        WriteStateTo(slot, supportedState);
    }
    public void Reset(ItemSlot slot) => WriteStateTo(slot, new(mInitialState));
    public static IState Construct(string state) => new State(state);

    private State ReadStateFrom(ItemSlot slot)
    {
        if (slot.Itemstack == null)
        {
            Logger.Error(mApi, this, $"ItemStack is null");
            return new(mInitialState);
        }

        State serverState = ReadStateFromServer(slot.Itemstack);

        if (mApi.Side == EnumAppSide.Server)
        {
            slot.MarkDirty();
            return serverState;
        }

        State clientState = ReadStateFromClient(slot.Itemstack);

#if DEBUG
        //if (clientState != serverState) Logger.Warn(mApi, this, $"State desync ({clientState == serverState}). Client: {clientState}, Server: {serverState}");
#endif

        if (clientState != serverState)
        {
            SynchronizeStates(slot.Itemstack, serverState);
        }
        else
        {
            CancelSynchronization(slot.Itemstack);
        }

        return clientState;
    }
    private void WriteStateTo(ItemSlot slot, State state)
    {
        if (slot.Itemstack == null)
        {
            Logger.Debug(mApi, this, $"ItemStack is null");
            return;
        }

        if (mApi.Side == EnumAppSide.Server)
        {
            WriteStateToServer(slot.Itemstack, state);
            slot.MarkDirty();
        }
        else
        {
            WriteStateToClient(slot.Itemstack, state);
        }
    }
    private State ReadStateFromClient(ItemStack stack)
    {
        if (!stack.TempAttributes.HasAttribute(cStateAttributeNameClient))
        {
            WriteStateToClient(stack, ReadStateFromServer(stack));
        }
        
        return State.Deserialize(stack.TempAttributes.GetAsString(cStateAttributeNameClient, mInitialState));
    }
    private static void WriteStateToClient(ItemStack stack, State state) => stack.TempAttributes.SetString(cStateAttributeNameClient, state.Serialize());
    private State ReadStateFromServer(ItemStack stack) => State.Deserialize(stack.Attributes.GetAsString(cStateAttributeNameServer, mInitialState));
    private static void WriteStateToServer(ItemStack stack, State state) => stack.Attributes.SetString(cStateAttributeNameServer, state.Serialize());

    private void SynchronizeStates(ItemStack stack, State serverState)
    {
        if (!CheckTimestamp(stack))
        {
            WriteTimestamp(stack);
            return;
        }

        if (ReadTimestamp(stack) > mSynchronizationDelay)
        {
            RemoveTimeStamp(stack);
            WriteStateToClient(stack, serverState);
        }
    }
    private void CancelSynchronization(ItemStack stack)
    {
        if (!CheckTimestamp(stack)) return;
        RemoveTimeStamp(stack);
    }
    private static void RemoveTimeStamp(ItemStack stack) => stack.TempAttributes.RemoveAttribute(cSyncAttributeName);
    private static bool CheckTimestamp(ItemStack stack) => stack.TempAttributes.HasAttribute(cSyncAttributeName);
    private void WriteTimestamp(ItemStack stack) => stack.TempAttributes.SetLong(cSyncAttributeName, mApi.World.ElapsedMilliseconds);
    private TimeSpan ReadTimestamp(ItemStack stack) => TimeSpan.FromMilliseconds(mApi.World.ElapsedMilliseconds - stack.TempAttributes.GetLong(cSyncAttributeName, 0));

    internal sealed class State : IState, IEquatable<State>
    {
        private readonly string mState;
        private readonly int mHash;

        public State(string state)
        {
            mState = state;
            mHash = mState.GetHashCode();
        }
        public override string ToString() { return $"{mState}"; }
        public override bool Equals(object? obj) => (obj as State)?.mHash == mHash;
        public bool Equals(State? other) => other?.mHash == mHash;
        public override int GetHashCode()
        {
            return mHash;
        }
        public string Serialize() => mState;
        public static State Deserialize(string state) => new(state);

        public static bool operator ==(State first, State second) => first.Equals(second);
        public static bool operator !=(State first, State second) => !first.Equals(second);
    }
}
