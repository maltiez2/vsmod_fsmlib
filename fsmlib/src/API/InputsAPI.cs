using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;



namespace MaltiezFSM.API;

public interface IInputManager : IDisposable
{
    public delegate bool InputCallback(ItemSlot slot, IPlayer player, IInput input);
    void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible);
    bool RegisterInvoker(IInputInvoker invoker, Type inputType);
}
public interface IInputInvoker : IDisposable
{
    public delegate bool InputCallback(SlotData slot, IPlayer player, IInput input, bool synchronize = true);
    void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible);
}
public interface IInput : IFactoryProduct
{
    public int Index { get; internal set; }
    public bool Handle { get; }
    public SlotType Slot { get; }
    CollectibleObject Collectible { get; }
    WorldInteraction? GetInteractionInfo(ItemSlot slot);
}
public interface ICustomInputInvoker
{
    public bool Invoke(string input, IPlayer player, ItemSlot? inSlot = null);
}
public interface IToolModeInvoker
{
    void Invoke(ItemSlot slot, IPlayer player, string id);
}
public interface IToolModeEventProvider
{
    event ToolModesGetter? OnGetToolModes;
}
public interface IActionInputInvoker
{
    bool IsActive(EnumEntityAction action, bool asModifier = false);
}

public struct KeyPressModifiers
{
    public bool? Alt { get; set; }
    public bool? Ctrl { get; set; }
    public bool? Shift { get; set; }
    public readonly HashSet<string> Codes => GetCodes();

    public KeyPressModifiers(bool? alt, bool? ctrl, bool? shift)
    {
        Alt = alt;
        Ctrl = ctrl;
        Shift = shift;
    }

    private readonly HashSet<string> GetCodes()
    {
        HashSet<string> codes = new();
        if (Alt == true) codes.Add("alt");
        if (Ctrl == true) codes.Add("ctrl");
        if (Shift == true) codes.Add("shift");
        return codes;
    }

    public override readonly string ToString()
    {
        StringBuilder result = new();

        result.Append("Modifiers: ");
        if (Alt == true) result.Append(" Alt");
        if (Ctrl == true) result.Append(" Ctrl");
        if (Shift == true) result.Append(" Shift");
        if (Alt == false) result.Append(" ~Alt");
        if (Ctrl == false) result.Append(" ~Ctrl");
        if (Shift == false) result.Append(" ~Shift");
        if (Alt == null && Ctrl == null && Shift == null) result.Append(" None");

        return result.ToString();
    }
}
