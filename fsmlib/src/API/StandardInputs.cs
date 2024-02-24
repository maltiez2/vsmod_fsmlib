using Vintagestory.API.Client;
using Vintagestory.API.Common;



namespace MaltiezFSM.API;

public interface IStandardInput : IInput
{
}
public interface IOperationInput : IStandardInput
{
    IOperation? Operation { get; set; }
    string OperationCode { get; set; }
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
    string Activity { get; }
    bool Invert { get; }
    StatusType Status { get; }
}
public interface IKeyInput : IStandardInput
{
    enum KeyEventType
    {
        KeyDown,
        KeyUp,
        KeyHold
    }
    KeyEventType EventType { get; }
    string HotKey { get; }
    string Name { get; }
    GlKeys Key { get; set; }
    KeyPressModifiers Modifiers { get; set; }
    bool CheckIfShouldBeHandled(KeyEvent keyEvent, KeyEventType eventType);
}
public interface IMouseInput : IStandardInput
{
    enum MouseEventType
    {
        MouseMove,
        MouseDown,
        MouseUp,
        MouseHold
    }
    MouseEventType EventType { get; }
    string Name { get; }
    KeyPressModifiers Modifiers { get; set; }
    EnumMouseButton Key { get; set; }
    bool CheckIfShouldBeHandled(MouseEvent mouseEvent, MouseEventType eventType);
}
public interface IActionInput : IStandardInput
{
    EnumEntityAction[] Actions { get; }
    bool OnRelease { get; }
    bool Modifiers { get; }
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
public interface IHotkeyInput : IStandardInput
{
    string[] Hotkeys { get; }
}
public interface ICustomInput : IStandardInput
{
    string Code { get; }
}
public interface ISlotContentInput : IStandardInput
{
    enum SlotEventType
    {
        SomeTaken,
        AllTaken,
        AfterModified
    }

    SlotEventType EventType { get; }
}

public interface IToolModeInput : IStandardInput
{
    string ModeId { get; }
}