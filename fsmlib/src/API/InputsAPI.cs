using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;



namespace MaltiezFSM.API;

public interface IInputManager : IDisposable
{
    public delegate bool InputCallback(ItemSlot slot, IPlayer player, IInput input);
    void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible);
    void RegisterInvoker(IInputInvoker invoker, Type inputType);
}
public interface IInputInvoker : IDisposable
{
    public delegate bool InputCallback(Utils.SlotData slot, IPlayer player, IInput input);
    void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible);
}
public interface IInput : IFactoryProduct
{
    public int Index { get; internal set; }
    public bool Handle { get; }
    public Utils.SlotType Slot { get; }
    WorldInteraction? GetInteractionInfo(ItemSlot slot);
}
public interface ICustomInputInvoker
{
    public bool Invoke(string input, IPlayer player, ItemSlot? inSlot = null);
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
}
