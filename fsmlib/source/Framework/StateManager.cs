using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

internal sealed class StateManager : IStateManager
{
    public static readonly System.Func<string, IState> DefaultDeserializer = State.Deserialize;

    public StateManager(ICoreAPI api, JsonObject behaviourAttributes, int id, System.Func<string, IState>? deserializer = null)
    {
        _api = api;
        _initialState = behaviourAttributes["initialState"].AsString();
        _deserialize = deserializer ?? DefaultDeserializer;
        _clientStateAttribute = $"{_stateAttributeNameClient}.{id}";
        _serverStateAttribute = $"{_stateAttributeNameServer}.{id}";
    }
    public StateManager(ICoreAPI api, string initialState, int id, System.Func<string, IState>? deserializer = null)
    {
        _api = api;
        _initialState = initialState;
        _deserialize = deserializer ?? DefaultDeserializer;
        _clientStateAttribute = $"{_stateAttributeNameClient}.{id}";
        _serverStateAttribute = $"{_stateAttributeNameServer}.{id}";
    }
    public IState DeserializeState(string state) => _deserialize(state);
    public IState Get(ItemSlot slot) => ReadStateFrom(slot);
    public void Set(ItemSlot slot, IState state)
    {
        /*if (state is not State supportedState)
        {
            Logger.Error(_api, this, $"Unsupported state class was passed: {state}");
            return;
        }*/
        WriteStateTo(slot, state);
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

public sealed class VectorState : IState, IEquatable<VectorState>
{
    public string[] Vector => _state.Split('-');

    private readonly int _dimension;
    private readonly string _state;
    private readonly int _hash;

    public VectorState(string state)
    {
        _state = state;
        _dimension = Vector.Length;
        _hash = _state.GetHashCode();
    }
    public VectorState(string[] state)
    {
        _dimension = state.Length;
        _state = state.Aggregate((first, second) => $"{first}-{second}");
        _hash = _state.GetHashCode();
    }
    public override string ToString() => _state;
    public override bool Equals(object? obj) => (obj as VectorState)?._hash == _hash;
    public bool Equals(IState? other) => other?.GetHashCode() == _hash;
    public bool Equals(VectorState? other) => other?._hash == _hash;
    public override int GetHashCode()
    {
        return _hash;
    }

    public string Serialize() => _state;
    public static VectorState Deserialize(string state) => new(state);
    public static VectorState Deserialize(string[] state) => new(state);
    public static VectorState Merge(VectorState first, VectorState second)
    {
        if (first._dimension != second._dimension) throw new ArgumentException($"Only vector states with same dimension can be merged.\nFirst state '{first}' dimension: '{first._dimension}'.\nSecond state '{second}' dimension: '{second._dimension}'.");

        List<string> newState = new();
        string[] firstVector = first.Vector;
        string[] secondVector = second.Vector;
        for (int index = 0; index < first._dimension; index++)
        {
            newState.Add(firstVector[index] == "*" ? secondVector[index] : firstVector[index]);
        }
        return new VectorState(newState.ToArray());
    }
    public static VectorState Substitute(VectorState state, int elementIndex, string value)
    {
        if (elementIndex >= state._dimension) throw new ArgumentException($"Tried to set state element with index '{elementIndex}' greater or equal to dimension '{state._dimension}' of state '{state}'.", nameof(elementIndex));
        string[] newState = state.Vector.Select((element, index) => index == elementIndex ? value : element).ToArray();
        return Deserialize(newState);
    }

    public static bool operator ==(VectorState first, VectorState second) => first.Equals(second);
    public static bool operator !=(VectorState first, VectorState second) => !first.Equals(second);
}

public sealed class VectorState2 : IState, IEquatable<VectorState2>
{
    private readonly int _hash;
    private readonly string _first;
    private readonly string _second;

    public VectorState2(string first, string second)
    {
        _first = first;
        _second = second;
        _hash = Serialize().GetHashCode();
    }
    public VectorState2(string full)
    {
        string[] parts = full.Split('-');
        if (parts.Length != 2) throw new ArgumentException($"VectorState2 should be string that can be splitted by '-' into only two string.\nSplitted length = {parts.Length}\nProvided string: '{full}'.");
        _first = parts[0];
        _second = parts[1];
        _hash = Serialize().GetHashCode();
    }
    public override string ToString() => Serialize();
    public override bool Equals(object? obj) => (obj as VectorState2)?._hash == _hash;
    public bool Equals(IState? other) => other?.GetHashCode() == _hash;
    public bool Equals(VectorState2? other) => other?._hash == _hash;
    public override int GetHashCode() => _hash;

    public string Serialize() => $"{_first}-{_second}";
    public static VectorState2 Deserialize(string state)
    {
        if (_statesCache.ContainsKey(state)) return _statesCache[state];
        _statesCache.Add(state, new(state));
        return _statesCache[state];
    }
    public static VectorState2 Merge(VectorState2 first, VectorState2 second)
    {
        string newState = (first._first == "*" ? second._first : first._first) + "-" + (first._second == "*" ? second._second : first._second);
        return Deserialize(newState);
    }
    public static VectorState2 Substitute(VectorState2 state, int elementIndex, string value)
    {
        return elementIndex switch
        {
            0 => Deserialize($"{value}-{state._second}"),
            1 => Deserialize($"{state._first}-{value}"),
            _ => throw new ArgumentException($"Tried to set state element with index '{elementIndex}' greater or equal to dimension '2' of state '{state}'.", nameof(elementIndex))
        };
    }

    public static bool operator ==(VectorState2 first, VectorState2 second) => first.Equals(second);
    public static bool operator !=(VectorState2 first, VectorState2 second) => !first.Equals(second);

    /// <summary>
    /// Cleared in mod system's Dispose method
    /// </summary>
    internal static readonly Dictionary<string, VectorState2> _statesCache = new();
}

public sealed class VectorState3 : IState, IEquatable<VectorState3>
{
    private readonly int _hash;
    private readonly string _first;
    private readonly string _second;
    private readonly string _third;

    public VectorState3(string first, string second, string third)
    {
        _first = first;
        _second = second;
        _third = third;
        _hash = Serialize().GetHashCode();

    }
    public VectorState3(string full)
    {
        string[] parts = full.Split('-');
        if (parts.Length != 3) throw new ArgumentException($"VectorState3 should be string that can be splitted by '-' into only three string.\nSplitted length = {parts.Length}\nProvided string: '{full}'.");
        _first = parts[0];
        _second = parts[1];
        _third = parts[2];
        _hash = Serialize().GetHashCode();
    }
    public override string ToString() => Serialize();
    public override bool Equals(object? obj) => (obj as VectorState3)?._hash == _hash;
    public bool Equals(IState? other) => other?.GetHashCode() == _hash;
    public bool Equals(VectorState3? other) => other?._hash == _hash;
    public override int GetHashCode() => _hash;

    public string Serialize() => $"{_first}-{_second}-{_third}";
    public static VectorState3 Deserialize(string state)
    {
        if (_statesCache.ContainsKey(state)) return _statesCache[state];
        _statesCache.Add(state, new(state));
        return _statesCache[state];
    }
    public static VectorState3 Merge(VectorState3 first, VectorState3 second)
    {
        string newState =
            (first._first == "*" ? second._first : first._first) +
            "-" +
            (first._second == "*" ? second._second : first._second) +
            "-" +
            (first._third == "*" ? second._third : first._third);
        return Deserialize(newState);
    }
    public static VectorState3 Substitute(VectorState3 state, int elementIndex, string value)
    {
        return elementIndex switch
        {
            0 => Deserialize($"{value}-{state._second}-{state._third}"),
            1 => Deserialize($"{state._first}-{value}-{state._third}"),
            2 => Deserialize($"{state._first}-{state._second}-{value}"),
            _ => throw new ArgumentException($"Tried to set state element with index '{elementIndex}' greater or equal to dimension '3' of state '{state}'.", nameof(elementIndex))
        };
    }

    public static bool operator ==(VectorState3 first, VectorState3 second) => first.Equals(second);
    public static bool operator !=(VectorState3 first, VectorState3 second) => !first.Equals(second);

    /// <summary>
    /// Cleared in mod system's Dispose method
    /// </summary>
    internal static readonly Dictionary<string, VectorState3> _statesCache = new();
}

public sealed class VectorState4 : IState, IEquatable<VectorState4>
{
    private readonly int _hash;
    private readonly string _first;
    private readonly string _second;
    private readonly string _third;
    private readonly string _fourth;

    public VectorState4(string first, string second, string third, string fourth)
    {
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
        _hash = Serialize().GetHashCode();

    }
    public VectorState4(string full)
    {
        string[] parts = full.Split('-');
        if (parts.Length != 4) throw new ArgumentException($"VectorState4 should be string that can be splitted by '-' into only four string.\nSplitted length = {parts.Length}\nProvided string: '{full}'.");
        _first = parts[0];
        _second = parts[1];
        _third = parts[2];
        _fourth = parts[3];
        _hash = Serialize().GetHashCode();
    }
    public override string ToString() => Serialize();
    public override bool Equals(object? obj) => (obj as VectorState4)?._hash == _hash;
    public bool Equals(IState? other) => other?.GetHashCode() == _hash;
    public bool Equals(VectorState4? other) => other?._hash == _hash;
    public override int GetHashCode() => _hash;

    public string Serialize() => $"{_first}-{_second}-{_third}-{_fourth}";
    public static VectorState4 Deserialize(string state)
    {
        if (_statesCache.ContainsKey(state)) return _statesCache[state];
        _statesCache.Add(state, new(state));
        return _statesCache[state];
    }
    public static VectorState4 Merge(VectorState4 first, VectorState4 second)
    {
        string newState =
            (first._first == "*" ? second._first : first._first) +
            "-" +
            (first._second == "*" ? second._second : first._second) +
            "-" +
            (first._third == "*" ? second._third : first._third) +
            "-" +
            (first._fourth == "*" ? second._fourth : first._fourth);
        return Deserialize(newState);
    }
    public static VectorState4 Substitute(VectorState4 state, int elementIndex, string value)
    {
        return elementIndex switch
        {
            0 => Deserialize($"{value}-{state._second}-{state._third}-{state._fourth}"),
            1 => Deserialize($"{state._first}-{value}-{state._third}-{state._fourth}"),
            2 => Deserialize($"{state._first}-{state._second}-{value}-{state._fourth}"),
            3 => Deserialize($"{state._first}-{state._second}-{state._third}-{value}"),
            _ => throw new ArgumentException($"Tried to set state element with index '{elementIndex}' greater or equal to dimension '4' of state '{state}'.", nameof(elementIndex))
        };
    }

    public static bool operator ==(VectorState4 first, VectorState4 second) => first.Equals(second);
    public static bool operator !=(VectorState4 first, VectorState4 second) => !first.Equals(second);

    /// <summary>
    /// Cleared in mod system's Dispose method
    /// </summary>
    internal static readonly Dictionary<string, VectorState4> _statesCache = new();
}