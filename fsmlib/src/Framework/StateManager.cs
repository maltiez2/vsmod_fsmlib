using MaltiezFSM.API;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using VSImGui;

namespace MaltiezFSM.Framework;

internal sealed class StateManager : IStateManager
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
    private readonly System.Func<string, IState> mDeserialize;

    public StateManager(ICoreAPI api, JsonObject behaviourAttributes)
    {
        mApi = api;
        mInitialState = behaviourAttributes["initialState"].AsString();
        mDeserialize = (string value) => new State(value);

#if DEBUG
        DebugWindow.IntSlider("fsmlib", "tweaks", "state sync delay", 0, 1000, () => (int)mSynchronizationDelay.TotalMilliseconds, value => mSynchronizationDelay = TimeSpan.FromMilliseconds(value));
#endif
    }
    public IState DeserializeState(string state) => mDeserialize(state);
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
    public void Reset(ItemSlot slot) => WriteStateTo(slot, mDeserialize(mInitialState));

    private IState ReadStateFrom(ItemSlot slot)
    {
        if (slot.Itemstack == null)
        {
            Logger.Error(mApi, this, $"ItemStack is null");
            return mDeserialize(mInitialState);
        }

        IState serverState = ReadStateFromServer(slot.Itemstack);

        if (mApi.Side == EnumAppSide.Server)
        {
            slot.MarkDirty();
            return serverState;
        }

        IState clientState = ReadStateFromClient(slot.Itemstack);

#if DEBUG
        //if (clientState != serverState) Logger.Warn(mApi, this, $"State desync ({clientState == serverState}). Client: {clientState}, Server: {serverState}");
#endif

        if (clientState != serverState)
        {
            clientState = SynchronizeStates(slot.Itemstack, serverState, clientState);
        }
        else
        {
            CancelSynchronization(slot.Itemstack);
        }

        return clientState;
    }
    private void WriteStateTo(ItemSlot slot, IState state)
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
    private IState ReadStateFromClient(ItemStack stack)
    {
        if (!stack.TempAttributes.HasAttribute(cStateAttributeNameClient))
        {
            WriteStateToClient(stack, ReadStateFromServer(stack));
        }

        return mDeserialize(stack.TempAttributes.GetAsString(cStateAttributeNameClient, mInitialState));
    }
    private static void WriteStateToClient(ItemStack stack, IState state) => stack.TempAttributes.SetString(cStateAttributeNameClient, state.Serialize());
    private IState ReadStateFromServer(ItemStack stack) => mDeserialize(stack.Attributes.GetAsString(cStateAttributeNameServer, mInitialState));
    private static void WriteStateToServer(ItemStack stack, IState state) => stack.Attributes.SetString(cStateAttributeNameServer, state.Serialize());

    private IState SynchronizeStates(ItemStack stack, IState serverState, IState clientState)
    {
        if (!CheckTimestamp(stack))
        {
            WriteTimestamp(stack);
            return clientState;
        }

        if (ReadTimestamp(stack) > mSynchronizationDelay)
        {
            RemoveTimeStamp(stack);
            WriteStateToClient(stack, serverState);
            return serverState;
        }

        return clientState;
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
}

internal sealed class State : IState, IEquatable<State>
{
    private readonly string mState;
    private readonly int mHash;

    public State(string state)
    {
        mState = state;
        mHash = mState.GetHashCode();
    }
    public override string ToString() => mState;
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