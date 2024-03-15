using MaltiezFSM.API;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework.Simplified;

public delegate bool InputHandlerDelegate(ItemSlot slot, IPlayer? player, IInput input, IState state);

public interface IFiniteStateMachine
{
    Dictionary<IState, Dictionary<IInput, InputHandlerDelegate>> Handlers { get; set; }

    event Action<ItemSlot, IState>? StateChanged;

    bool SetState(ItemSlot slot, IState state);
    bool SetState(ItemSlot slot, string state);
    IState GetState(ItemSlot slot);
    IState DeserializeState(string state);
    IEnumerable<IState> ResolverState(string wildcard);
}

public interface IFiniteStateMachineAttributesBased : IFiniteStateMachine
{
    bool Init(object owner, CollectibleObject collectible);
    bool SetState(ItemSlot slot, int elementIndex, string value);
    bool SetState(ItemSlot slot, params (int elementIndex, string value)[] elements);
    bool CheckState(ItemSlot slot, int elementIndex, string wildcard);
}