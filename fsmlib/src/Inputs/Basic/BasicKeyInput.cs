using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IKeyInput;
using MaltiezFSM.API;
using Vintagestory.API.Client;
using System;
using System.Linq;

#nullable enable

namespace MaltiezFSM.Inputs
{
    public class KeyboardKey : BaseInput, IKeyInput
    {
        private readonly string mHotkey;
        private readonly string mLangName;
        private readonly KeyEventType mType;

        private string mKey;
        private int mKeyEnum;
        private KeyPressModifiers mModifiers;

        public KeyboardKey(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
        {
            mKey = definition["key"].AsString();
            mHotkey = definition["hotkey"].AsString();
            mLangName = definition["name"].AsString(code);
            mKeyEnum = (int)Enum.Parse(typeof(GlKeys), mKey);
            switch (definition["type"].AsString())
            {
                case ("released"):
                    mType = KeyEventType.KeyUp;
                    break;
                case ("pressed"):
                    mType = KeyEventType.KeyDown;
                    break;
                default:
                    mType = KeyEventType.KeyDown;
                    break;

            }
            mModifiers = new KeyPressModifiers
            (
                definition.KeyExists("alt") ? definition["alt"].AsBool(false) : null,
                definition.KeyExists("ctrl") ? definition["ctrl"].AsBool(false) : null,
                definition.KeyExists("shift") ? definition["shift"].AsBool(false) : null
            );
        }

        public KeyEventType GetEventType()
        {
            return mType;
        }
        public bool CheckIfShouldBeHandled(KeyEvent keyEvent, KeyEventType eventType)
        {
            if (mType != eventType) return false;
            if (keyEvent.KeyCode != mKeyEnum && keyEvent.KeyCode2 != mKeyEnum) return false;
            if (mModifiers.Alt != null && keyEvent.AltPressed != mModifiers.Alt) return false;
            if (mModifiers.Ctrl != null && keyEvent.CtrlPressed != mModifiers.Ctrl) return false;
            if (mModifiers.Shift != null && keyEvent.ShiftPressed != mModifiers.Shift) return false;

            return true;
        }

        public KeyPressModifiers GetModifiers()
        {
            return mModifiers;
        }
        public string GetKey()
        {
            return mKey;
        }

        public string GetHotkeyCode()
        {
            return mHotkey;
        }

        public string GetLangName()
        {
            return mLangName;
        }

        public void SetModifiers(KeyPressModifiers modifiers)
        {
            mModifiers = modifiers;
        }

        public void SetKey(string key)
        {
            mKey = key;
            mKeyEnum = (int)Enum.Parse(typeof(GlKeys), mKey);
        }
        public override WorldInteraction GetInteractionInfo(ItemSlot slot)
        {
            return new WorldInteraction()
            {
                ActionLangCode = mLangName,
                MouseButton = EnumMouseButton.None,
                HotKeyCodes = mModifiers.Codes.Append(mHotkey).ToArray()
            };
        }
    }
}
