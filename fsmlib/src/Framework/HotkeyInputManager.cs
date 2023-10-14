using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using MaltiezFSM.API;
using Vintagestory.API.Config;

namespace MaltiezFSM.Framework
{
    public class HotkeyInputManager : IHotkeyInputManager
    {
        private readonly ICoreClientAPI mClientApi;

        private readonly Dictionary<string, List<IHotkeyInputManager.InputCallback>> mCallbacks = new();

        public HotkeyInputManager(ICoreClientAPI api)
        {
            mClientApi = api;
        }

        void IHotkeyInputManager.RegisterHotkeyInput(IHotkeyInput input, IHotkeyInputManager.InputCallback callback)
        {
            string code = input.GetName();

            mCallbacks.TryAdd(code, new());
            mCallbacks[code].Add(callback);

            RegisterHotkey(input, code);
        }

        private void RegisterHotkey(IHotkeyInput input, string code)
        {
            KeyPressModifiers altCtrlShift = input.GetModifiers();
            GlKeys key = (GlKeys)Enum.Parse(typeof(GlKeys), input.GetKey());

            mClientApi.Input.RegisterHotKey(code, Lang.Get(input.GetLangName()), key, HotkeyType.CharacterControls, altCtrlShift.AltAsBool(), altCtrlShift.CtrlAsBool(), altCtrlShift.ShiftAsBool());
            mClientApi.Input.SetHotKeyHandler(code, (KeyCombination keys) => ProxyHandler(keys, code));
        }

        private bool ProxyHandler(KeyCombination keys, string code)
        {
            foreach (IHotkeyInputManager.InputCallback callback in mCallbacks[code])
            {
                if (callback(keys))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
