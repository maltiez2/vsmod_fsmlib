using Vintagestory.API.Client;
using Vintagestory.API.Common;

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
    StatusType GetStatusType();
    bool CheckStatus();
}
public interface IKeyInput : IStandardInput, IKeyRelated
{
    enum KeyEventType
    {
        KeyDown,
        KeyUp
    }
    KeyEventType GetEventType();
    bool CheckIfShouldBeHandled(KeyEvent keyEvent, KeyEventType eventType);
    string GetHotkeyCode();
    string GetLangName();
    void SetKey(string key);
}
public interface IMouseInput : IStandardInput, IKeyRelated
{
    enum MouseEventType
    {
        MouseMove,
        MouseDown,
        MouseUp
    }
    MouseEventType GetEventType();
    bool CheckIfShouldBeHandled(MouseEvent mouseEvent, MouseEventType eventType);
}
public interface ISlotInput : IStandardInput
{
    enum SlotEventType
    {
        FromSlot,
        ToSlot
    }
    SlotEventType GetEventType();
}
public interface ISlotChangedAfter : ISlotInput
{

}
public interface ISlotChangedBefore : ISlotInput
{
    EnumHandling GetHandlingType();
}
public interface IItemDropped : IStandardInput
{
}
