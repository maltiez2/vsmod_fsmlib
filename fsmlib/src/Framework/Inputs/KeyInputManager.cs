using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace MaltiezFSM.Framework
{
    public class KeyInputManager : IKeyInputManager
    {
        private readonly Dictionary<string, List<IKeyInput>> mInputs = new();

        private readonly ICoreClientAPI mClientApi;

        public KeyInputManager(ICoreClientAPI api)
        {
            mClientApi = api;

            SetUpListener();
        }

        public void RegisterKeyInput(IKeyInput input)
        {
            string hotkeyCode = input.GetHotkeyCode();
            if (hotkeyCode == null) return;

            KeyPressModifiers modifiers = input.GetModifiers();
            GlKeys key = (GlKeys)Enum.Parse(typeof(GlKeys), input.GetKey());

            if (!mInputs.ContainsKey(hotkeyCode))
            {
                mInputs.Add(hotkeyCode, new());
                mClientApi.Input.RegisterHotKey(hotkeyCode, input.GetLangName(), key, HotkeyType.CharacterControls, modifiers.Alt == true, modifiers.Ctrl == true, modifiers.Shift == true);
            }
            
            mInputs[hotkeyCode].Add(input);
        }

        private void SetUpListener()
        {
            mClientApi.Event.HotkeysChanged += HotkeysChangedCallback;
            mClientApi.Event.PauseResume += _ => HotkeysChangedCallback();
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
            int key = (int)Enum.Parse(typeof(GlKeys), input.GetKey());
            KeyPressModifiers modifiers = input.GetModifiers();
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
            KeyPressModifiers modifiers = input.GetModifiers();
            string key = ((GlKeys)hotkeyCombination.KeyCode).ToString();

            if (modifiers.Alt != false) modifiers.Alt = hotkeyCombination.Alt ? true : null;
            if (modifiers.Ctrl != false) modifiers.Ctrl = hotkeyCombination.Ctrl ? true : null;
            if (modifiers.Shift != false) modifiers.Shift = hotkeyCombination.Shift ? true : null;

            input.SetModifiers(modifiers);
            input.SetKey(key);
        }
    }
}
