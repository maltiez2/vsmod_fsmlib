using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

internal sealed class StateManager : IStateManager
{
    public StateManager(ICoreAPI api, JsonObject behaviourAttributes, int id)
    {
        _api = api;
        _initialState = behaviourAttributes["initialState"].AsString();
        _deserialize = (string value) => new State(value);
        _clientStateAttribute = $"{_stateAttributeNameClient}.{id}";
        _serverStateAttribute = $"{_stateAttributeNameServer}.{id}";
    }
    public IState DeserializeState(string state) => _deserialize(state);
    public IState Get(ItemSlot slot) => ReadStateFrom(slot);
    public void Set(ItemSlot slot, IState state)
    {
        if (state is not State supportedState)
        {
            Logger.Error(_api, this, $"Unsupported state class was passed: {state}");
            return;
        }
        WriteStateTo(slot, supportedState);
    }
    public void Reset(ItemSlot slot) => WriteStateTo(slot, _deserialize(_initialState));


    private const string _stateAttributeNameClient = "FSMlib.state.client";
    private const string _stateAttributeNameServer = "FSMlib.state.server";
    private const string _syncAttributeName = "FSMlib.sync";
    private readonly TimeSpan _synchronizationDelay = TimeSpan.FromMilliseconds(90);
    private readonly string _initialState;
    private readonly ICoreAPI _api;
    private readonly System.Func<string, IState> _deserialize;
    private readonly string _clientStateAttribute;
    private readonly string _serverStateAttribute;

    private IState ReadStateFrom(ItemSlot slot)
    {
        if (slot.Itemstack == null)
        {
            Logger.Error(_api, this, $"ItemStack is null");
            return _deserialize(_initialState);
        }

        IState serverState = ReadStateFromServer(slot.Itemstack);

        if (_api.Side == EnumAppSide.Server)
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
            Logger.Debug(_api, this, $"ItemStack is null");
            return;
        }

        if (_api.Side == EnumAppSide.Server)
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
        if (!stack.TempAttributes.HasAttribute(_clientStateAttribute))
        {
            WriteStateToClient(stack, ReadStateFromServer(stack));
        }

        return _deserialize(stack.TempAttributes.GetAsString(_clientStateAttribute, _initialState));
    }
    private void WriteStateToClient(ItemStack stack, IState state) => stack.TempAttributes.SetString(_clientStateAttribute, state.Serialize());
    private IState ReadStateFromServer(ItemStack stack) => _deserialize(stack.Attributes.GetAsString(_serverStateAttribute, _initialState));
    private void WriteStateToServer(ItemStack stack, IState state) => stack.Attributes.SetString(_serverStateAttribute, state.Serialize());

    private IState SynchronizeStates(ItemStack stack, IState serverState, IState clientState)
    {
        if (!CheckTimestamp(stack))
        {
            WriteTimestamp(stack);
            return clientState;
        }

        if (ReadTimestamp(stack) > _synchronizationDelay)
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
    private static void RemoveTimeStamp(ItemStack stack) => stack.TempAttributes.RemoveAttribute(_syncAttributeName);
    private static bool CheckTimestamp(ItemStack stack) => stack.TempAttributes.HasAttribute(_syncAttributeName);
    private void WriteTimestamp(ItemStack stack) => stack.TempAttributes.SetLong(_syncAttributeName, _api.World.ElapsedMilliseconds);
    private TimeSpan ReadTimestamp(ItemStack stack) => TimeSpan.FromMilliseconds(_api.World.ElapsedMilliseconds - stack.TempAttributes.GetLong(_syncAttributeName, 0));
}

internal sealed class State : IState, IEquatable<State>
{
    private readonly string _state;
    private readonly int _hash;

    public State(string state)
    {
        _state = state;
        _hash = _state.GetHashCode();
    }
    public override string ToString() => _state;
    public override bool Equals(object? obj) => (obj as State)?._hash == _hash;
    public bool Equals(IState? other) => other?.GetHashCode() == _hash;
    public bool Equals(State? other) => other?._hash == _hash;
    public override int GetHashCode()
    {
        return _hash;
    }
    public string Serialize() => _state;
    public static State Deserialize(string state) => new(state);

    public static bool operator ==(State first, State second) => first.Equals(second);
    public static bool operator !=(State first, State second) => !first.Equals(second);
}