using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.NewFramework;

public delegate bool TransitionDelegate(ItemSlot slot, IPlayer player);

public interface ITransition
{
    IState Tail {  get; }
    IState Head { get; }
    IInput Trigger { get; }
    TransitionDelegate Process { get; }
}

public interface IOperation : IFactoryProduct, IDisposable
{
    void Initialize(Dictionary<string, ISystem> systems);
    IEnumerable<ITransition> GetTransitions(IStateManager stateManager);
}

public interface ISystem : IFactoryProduct, IDisposable
{
    void Initialize(Dictionary<string, ISystem> systems);
    bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters);
    bool Process(ItemSlot slot, IPlayer player, JsonObject parameters);
    string[]? GetDescription(ItemSlot slot, IWorldAccessor world);
}