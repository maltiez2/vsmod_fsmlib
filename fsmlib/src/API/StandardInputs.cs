﻿using Vintagestory.API.Client;
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
        KeyUp
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
        MouseUp
    }
    MouseEventType EventType { get; }
    string Name { get; }
    KeyPressModifiers Modifiers { get; set; }
    EnumMouseButton Key { get; set; }
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
public interface ICustomInput : IStandardInput
{
    string Code { get; }
}