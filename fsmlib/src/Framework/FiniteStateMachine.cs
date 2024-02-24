using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using VSImGui;

namespace MaltiezFSM.Framework;

internal sealed class FiniteStateMachine : IFiniteStateMachine
{
    private readonly Dictionary<IState, Dictionary<IInput, List<IOperation>>> mOperationsByInputAndState = new();
    private readonly Dictionary<IOperation, HashSet<IState>> mStatesByOperationForTimer = new();
    private readonly Dictionary<long, Utils.DelayedCallback> mTimers = new();
    private readonly List<IOperation> mOperations = new();
    private readonly IOperationInputInvoker? mOperationInputInvoker;
    private readonly CollectibleObject mCollectible;
    private readonly IStateManager mStateManager;
    private readonly ICoreAPI mApi;

    private bool mDisposed = false;

    /// <summary>
    /// Manages timers and handles inputs. The only internal state mutating after initializing is timers per entity.<br/>
    /// Provides system to other systems. Gets transitions from operations and gives back systems, inputs and states.
    /// </summary>
    /// <param name="api"></param>
    /// <param name="operations">Instantiated operations.</param>
    /// <param name="systems">Instantiated systems.</param>
    /// <param name="inputs">Instantiated inputs.</param>
    /// <param name="collectible">Collectible this FSM attached to.</param>
    /// <param name="invoker">Is used to invoke inputs about start amd finish of operations managed by this FSM.</param>
    /// <param name="stateManager">Used to get and set states of item stacks in provided slots.</param>
    public FiniteStateMachine(
        ICoreAPI api,
        Dictionary<string, IOperation> operations,
        Dictionary<string, ISystem> systems,
        Dictionary<string, IInput> inputs,
        CollectibleObject collectible,
        IOperationInputInvoker? invoker,
        IStateManager stateManager
    )
    {
        mCollectible = collectible;
        mOperationInputInvoker = invoker;
        mStateManager = stateManager;
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
                List<IOperation.Transition> transitions = operation.GetTransitions(stateManager);
                ProcessTransitions(operation, transitions, inputs);
                operation.SetInputsStatesSystems(inputs, systems, stateManager);
                mOperations.Add(operation);
            }
            catch (Exception exception)
            {
                Logger.Error(api, this, $"Exception on processing transitions for '{operationCode}' operation for collectible '{mCollectible.Code}'.");
                Logger.Verbose(api, this, $"Exception on processing transitions for '{operationCode}' operation for collectible '{mCollectible.Code}'.\n\nException:\n{exception}\n");
            }
        }

        Logger.Verbose(api, this, $"Initialized for '{collectible.Code}'. States: {mOperationsByInputAndState.Count}, operations: {mOperations.Count}");
    }

    /// <summary>
    /// Returns available inputs for state of item in given slot
    /// </summary>
    /// <param name="slot">Slot containing collectible with FSM attached to it</param>
    /// <returns>List of available inputs for this state. Operations still may fail verification stage for this inputs and nit be performed.</returns>
    public List<IInput> GetAvailableInputs(ItemSlot slot)
    {
        List<IInput> inputs = new();

        IState state = mStateManager.Get(slot);

        if (!mOperationsByInputAndState.ContainsKey(state))
        {
            Logger.Error(mApi, this, $"Error on getting available inputs for '{state}' state for collectible '{mCollectible.Code}': state was not found. States registered: {mOperationsByInputAndState.Count}");
            return inputs;
        }

        foreach ((IInput item, _) in mOperationsByInputAndState[state])
        {
            inputs.Add(item);
        }

        return inputs;
    }
    
    /// <summary>
    /// Verifies and performs operations for collectible in slot corresponding to input-state pair, where state is current state of collectible in slot.
    /// Player is passed to operation, should not be null. If current item state does not correspond to any operation it will be reset to initial one.
    /// </summary>
    /// <param name="slot">Slot that contains collectible with this FSM attached. If it does not contain such collectible false will be returned, but it not the only outcome that returns false.</param>
    /// <param name="player">Player associated with given slot and input. If null will return false, but it not the only outcome that returns false.</param>
    /// <param name="input">Is used to determine from it and current item state what operation to perform.</param>
    /// <returns>Returns if supplied input was successfully handled by some operation</returns>
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

        foreach (IOperation operation in mOperationsByInputAndState[state][input])
        {
            try
            {
                if (RunOperation(slot, player, operation, input, state))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"Exception on running operation '{operation}' for input '{input}' in state '{state}' for collectible '{mCollectible.Code}'.");
                Logger.Debug(mApi, this, $"Exception on running operation '{operation}' for input '{input}' in state '{state}' for collectible '{mCollectible.Code}'.\nException:\n{exception}");
            }
        }

        return false;
    }
    
    /// <summary>
    /// If operation requested timer this is its timer callback. It will check if current state correspond to timer and then run supplied operation. Used primarily for timed operations like 'Delayed'.
    /// </summary>
    /// <param name="slot">Same that was provided to FSM at the moment of timer creation.</param>
    /// <param name="player">Same that was provided to FSM at the moment of timer creation.</param>
    /// <param name="input">Same that was provided to FSM at the moment of timer creation.</param>
    /// <param name="operation">Operation corresponding to this timer</param>
    private void OnTimer(ItemSlot slot, IPlayer player, IInput input, IOperation operation)
    {
        if (slot?.Itemstack?.Collectible != mCollectible || player == null) return;

        IState state = mStateManager.Get(slot);
        if (!mStatesByOperationForTimer.ContainsKey(operation) || !mStatesByOperationForTimer[operation].Contains(state))
        {
            return;
        }

        RunOperation(slot, player, operation, input, state);
    }

    /// <summary>
    /// Verifies and then performs given operation. Creates timer if needed. Invokes operation inputs using <see cref="IOperationInputInvoker"/>.
    /// </summary>
    /// <param name="slot">Slot with collectible this FSM attached to</param>
    /// <param name="player">Player associated with slot and input</param>
    /// <param name="operation">Operation to verify and perform</param>
    /// <param name="input">Input that corresponds to supplied operation and state</param>
    /// <param name="state">Current state of collectible in slot</param>
    /// <returns>Returns if supplied input was successfully handled by operation</returns>
    private bool RunOperation(ItemSlot slot, IPlayer player, IOperation operation, IInput input, IState state)
    {
        if (player == null || slot == null) return false;

        IOperation.Outcome outcome = operation.Verify(slot, player, state, input);

        if (outcome == IOperation.Outcome.Failed)
        {
            OperationsDebugWindow.Enqueue(operation, player, input, state, state, outcome, mApi.Side == EnumAppSide.Client); // Does nothing in RELEASE configuration
            return false;
        }

        if ((outcome == IOperation.Outcome.Started || outcome == IOperation.Outcome.StartedAndFinished) && mOperationInputInvoker?.Started(operation, slot, player) == true)
        {
            OperationsDebugWindow.Enqueue(operation, player, input, state, state, outcome, mApi.Side == EnumAppSide.Client); // Does nothing in RELEASE configuration
            return false;
        }

        IOperation.Result result = operation.Perform(slot, player, state, input);

        OperationsDebugWindow.Enqueue(operation, player, input, state, result.State, result.Outcome, mApi.Side == EnumAppSide.Client, result); // Does nothing in RELEASE configuration

        mStateManager.Set(slot, result.State);

        if (result.Timeout != IOperation.Timeout.Ignore && mTimers.ContainsKey(player.Entity.EntityId)) mTimers[player.Entity.EntityId]?.Cancel();

        if (result.Timeout == IOperation.Timeout.Start) mTimers[player.Entity.EntityId] = new(mApi, result.TimeoutDelay, () => OnTimer(slot, player, input, operation));

        if (result.Outcome == IOperation.Outcome.Finished) mOperationInputInvoker?.Finished(operation, slot, player);

        return input.Handle;
    }

    /// <summary>
    /// Adds operation to mapping from inputs and states
    /// </summary>
    /// <param name="operation">Operation that provided given transitions</param>
    /// <param name="transitions">Transitions provided by given operation</param>
    /// <param name="inputs">Registered inputs</param>
    private void ProcessTransitions(IOperation operation, List<IOperation.Transition> transitions, Dictionary<string, IInput> inputs)
    {
        foreach (IOperation.Transition transition in transitions)
        {
            IState initialState = mStateManager.DeserializeState(transition.FromState);
            IState finalState = mStateManager.DeserializeState(transition.ToState);

            mOperationsByInputAndState.TryAdd(initialState, new());
            mOperationsByInputAndState.TryAdd(finalState, new());

            if (transition.Trigger == IOperation.Transition.TriggerType.Timeout)
            {
                mStatesByOperationForTimer.TryAdd(operation, new());
                mStatesByOperationForTimer[operation].Add(initialState);
            }
            else if (transition.Trigger == IOperation.Transition.TriggerType.Input && transition.Input != null)
            {
                IInput input = inputs[transition.Input];

                if (!mOperationsByInputAndState[initialState].ContainsKey(input))
                {
                    mOperationsByInputAndState[initialState].Add(input, new());
                }

                mOperationsByInputAndState[initialState][input].Add(operation);
            }
        }
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
