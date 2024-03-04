using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace MaltiezFSM.Framework;

public sealed class KeyToHotkeyMapper : IDisposable
{
    private readonly Dictionary<string, List<IKeyInput>> mInputs = new();

    private readonly ICoreClientAPI mClientApi;
    private bool mDisposed;

    public KeyToHotkeyMapper(ICoreClientAPI api)
    {
        mClientApi = api;

        SetUpListener();
    }

    public void RegisterKeyInput(IKeyInput input)
    {
        string hotkeyCode = input.HotKey;
        if (hotkeyCode == null) return;

        KeyPressModifiers modifiers = input.Modifiers;


        if (!mInputs.ContainsKey(hotkeyCode))
        {
            mInputs.Add(hotkeyCode, new());
            mClientApi.Input.RegisterHotKey(hotkeyCode, Lang.Get(input.Name), input.Key, HotkeyType.CharacterControls, modifiers.Alt == true, modifiers.Ctrl == true, modifiers.Shift == true);
        }

        mInputs[hotkeyCode].Add(input);
    }

    private void SetUpListener()
    {
        mClientApi.Event.HotkeysChanged += HotkeysChangedCallback;
        mClientApi.Event.LevelFinalize += HotkeysChangedCallback;
    }

    private void HotkeysChangedCallback()
    {
        foreach ((string hotkeyCode, List<IKeyInput> inputs) in mInputs)
        {
            HotKey hotkey = mClientApi.Input.HotKeys[hotkeyCode];

            if (!CompareHotkey(inputs[0], hotkey))
            {
                foreach (IKeyInput input in inputs)
                {
                    UpdateInput(input, hotkey);
                }
            }
        }
    }

    private bool CompareHotkey(IKeyInput input, HotKey hotkey)
    {
        int key = (int)input.Key;
        KeyPressModifiers modifiers = input.Modifiers;
        KeyCombination hotkeyCombination = hotkey.CurrentMapping;

        if (hotkeyCombination.KeyCode != key) return false;
        if (modifiers.Alt != null && modifiers.Alt != hotkeyCombination.Alt) return false;
        if (modifiers.Ctrl != null && modifiers.Ctrl != hotkeyCombination.Ctrl) return false;
        if (modifiers.Shift != null && modifiers.Shift != hotkeyCombination.Shift) return false;

        return true;
    }

    private void UpdateInput(IKeyInput input, HotKey hotkey)
    {
        KeyCombination hotkeyCombination = hotkey.CurrentMapping;
        KeyPressModifiers modifiers = input.Modifiers;

        if (modifiers.Alt != false) modifiers.Alt = hotkeyCombination.Alt ? true : null;
        if (modifiers.Ctrl != false) modifiers.Ctrl = hotkeyCombination.Ctrl ? true : null;
        if (modifiers.Shift != false) modifiers.Shift = hotkeyCombination.Shift ? true : null;

        input.Modifiers = modifiers;
        input.Key = (GlKeys)hotkeyCombination.KeyCode;
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            mClientApi.Event.HotkeysChanged -= HotkeysChangedCallback;
            mClientApi.Event.LevelFinalize -= HotkeysChangedCallback;

            mDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
