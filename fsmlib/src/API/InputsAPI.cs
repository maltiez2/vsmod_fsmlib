using MaltiezFSM.Framework;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MaltiezFSM.API;

public interface IInputManager : IDisposable
{
    public delegate bool InputCallback(ItemSlot slot, EntityAgent player, IInput input);
    void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible);
    void RegisterInvoker(IInputInvoker invoker, Type inputType);
}
public interface IInputInvoker : IDisposable
{
    public delegate bool InputCallback(Utils.SlotData slot, IPlayer player, IInput input);
    void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible);
}

public interface IKeyRelatedInput
{
    KeyPressModifiers GetModifiers();
    string GetKey();
    void SetModifiers(KeyPressModifiers modifiers);
}
public interface IInput : IFactoryObject
{
    public int Index { get; set; }
    string GetName();
    bool Handled();
    Utils.SlotType SlotType();
    WorldInteraction GetInteractionInfo(ItemSlot slot);
}
public interface IOperationInput : IInput
{
    IOperation Operation { get; set; }
}
public interface IOperationStarted : IOperationInput
{

}
public interface IOperationFinished : IOperationInput
{

}
public interface IStatusInput : IInput
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
public interface IHotkeyInput : IInput, IKeyRelatedInput
{
    string GetLangName();
}
public interface IEventInput : IInput
{

}
public interface IKeyInput : IEventInput, IKeyRelatedInput
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
public interface IMouseInput : IEventInput, IKeyRelatedInput
{
    enum MouseEventType
    {
        MouseMove,
        MouseDown,
        MouseUp
    }
    MouseEventType GetEventType();
    bool CheckIfShouldBeHandled(MouseEvent mouseEvent, MouseEventType eventType);
    bool IsRepeatable();
}
public interface ISlotInput : IEventInput
{
    enum SlotEventType
    {
        FromSlot,
        ToSlot
    }
}
public interface ISlotChangedAfter : ISlotInput
{
    SlotEventType GetEventType();
}
public interface ISlotChangedBefore : ISlotInput
{
    SlotEventType GetEventType();
    EnumHandling GetHandlingType();
}
public interface IItemDropped : ISlotInput
{
}
