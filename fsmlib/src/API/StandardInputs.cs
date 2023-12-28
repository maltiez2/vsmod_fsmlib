using Vintagestory.API.Client;
using Vintagestory.API.Common;
using static MaltiezFSM.API.IKeyInput;

#nullable enable

namespace MaltiezFSM.API;

public interface IStandardInput : IInput
{
}
public interface IOperationInput : IStandardInput
{
    IOperation Operation { get; set; }
}
public interface IOperationStarted : IOperationInput
{

}
public interface IOperationFinished : IOperationInput
{

}
public interface IStatusInput : IStandardInput
{
    enum StatusType
    {
        Activity,
        Swimming,
        OnFire,
        Collided,
        CollidedHorizontally,
        CollidedVertically,
        EyesSubmerged,
        FeetInLiquid,
        InLava,
        OnGround
    }
    string Activity { get; set; }
    bool Invert { get; set; }
    StatusType Status { get; set; }
}
public interface IKeyInput : IStandardInput, IKeyPress
{
    enum KeyEventType
    {
        KeyDown,
        KeyUp
    }
    KeyEventType EventType { get; }
    string HotKey { get; }
    string Name { get; }
    bool CheckIfShouldBeHandled(KeyEvent keyEvent, KeyEventType eventType);
}
public interface IMouseInput : IStandardInput, IKeyPress
{
    enum MouseEventType
    {
        MouseMove,
        MouseDown,
        MouseUp
    }
    KeyEventType EventType { get; }
    bool CheckIfShouldBeHandled(MouseEvent mouseEvent, MouseEventType eventType);
}
public interface ISlotInput : IStandardInput
{
    enum SlotEventType
    {
        FromSlot,
        ToSlot
    }
    SlotEventType EventType { get; }
}
public interface ISlotChangedAfter : ISlotInput
{

}
public interface ISlotChangedBefore : ISlotInput
{
    EnumHandling Handling { get; }
}
public interface IItemDropped : IStandardInput
{
}
