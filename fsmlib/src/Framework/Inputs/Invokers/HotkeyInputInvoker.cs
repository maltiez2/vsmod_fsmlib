using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using VSImGui;

namespace MaltiezFSM.Framework;

public sealed class HotkeyInputInvoker : IInputInvoker
{
    private readonly ICoreClientAPI mClientApi;
    private readonly Dictionary<IHotkeyInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IHotkeyInput, CollectibleObject> mCollectibles = new();
    private bool mDispose = false;

    private readonly HashSet<string> mHotkeys = new();

    public HotkeyInputInvoker(ICoreClientAPI api)
    {
        mClientApi = api;
        mClientApi.Event.KeyDown += KeyPressListener;
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is not IHotkeyInput slotInput) return;
        
        mCallbacks.Add(slotInput, callback);
        mCollectibles.Add(slotInput, collectible);
        foreach (string hotkey in slotInput.Hotkeys.Where(value => !mHotkeys.Contains(value)))
        {
            mHotkeys.Add(hotkey);
        }
    }

    private void KeyPressListener(KeyEvent ev)
    {
        if (mClientApi.World?.Player?.Entity == null) return;

        foreach (string hotkeyId in mHotkeys)
        {
            if (mClientApi.Input.HotKeys.ContainsKey(hotkeyId) && CompareCombinations(ev, mClientApi.Input.HotKeys[hotkeyId].CurrentMapping))
            {
                HotkeyPressHandler(ev, hotkeyId);
                break;
            }
        }
    }

    private static bool CompareCombinations(KeyEvent A, KeyCombination B)
    {
        if (A.KeyCode != B.KeyCode) return false;
        if (A.ShiftPressed != B.Shift) return false;
        if (A.CtrlPressed != B.Ctrl) return false;
        if (A.AltPressed != B.Alt) return false;

        return true;
    }

    private void HotkeyPressHandler(KeyEvent ev, string hotkey)
    {
        bool handled = false;

        foreach (IHotkeyInput input in mCallbacks.Select(entry => entry.Key).Where(input => input.Hotkeys.Contains(hotkey)))
        {
            handled = HandleInput(input);
        }

        if (handled) ev.Handled = true;
    }

    private bool HandleInput(IHotkeyInput input)
    {
        SlotType slotType = input.Slot;

        IEnumerable<SlotData> slots = SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

        bool handled = false;
        foreach (SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input)))
        {
            handled = true;
        }

        return handled;
    }

    public void Dispose()
    {
        if (mDispose) return;
        mDispose = true;
        mClientApi.Event.KeyDown -= KeyPressListener;
    }
}
