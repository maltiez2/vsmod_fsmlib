using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IKeyInput;
using MaltiezFSM.API;
using Vintagestory.API.Client;
using System;
using HarmonyLib;
using System.Linq;

namespace MaltiezFSM.Inputs
{
    public class BasicKey : BaseInput, IKeyInput
    {
        public const string keyAttrName = "key";
        public const string keyPressTypeAttrName = "type";
        public const string altAttrName = "alt";
        public const string ctrlAttrName = "ctrl";
        public const string shiftAttrName = "shift";
        public const string hotkeyAttrName = "hotkey";
        public const string nameAttrName = "name";

        private string mKey;
        private string mHotkey;
        private string mLangName;
        private KeyEventType mType;
        private int mKeyEnum;
        private KeyPressModifiers mModifiers;
        private ICoreClientAPI mClientApi;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mKey = definition[keyAttrName].AsString();
            mHotkey = definition[hotkeyAttrName].AsString();
            mLangName = definition[nameAttrName].AsString(code);
            mKeyEnum = (int)Enum.Parse(typeof(GlKeys), mKey);
            switch (definition[keyPressTypeAttrName].AsString())
            {
                case ("released"):
                    mType = KeyEventType.KEY_UP;
                    break;
                case ("pressed"):
                    mType = KeyEventType.KEY_DOWN;
                    break;
                default:
                    mType = KeyEventType.KEY_DOWN;
                    break;

            }
            mModifiers = new KeyPressModifiers
            (
                definition.KeyExists(altAttrName) ? definition[altAttrName].AsBool(false) : null,
                definition.KeyExists(ctrlAttrName) ? definition[ctrlAttrName].AsBool(false) : null,
                definition.KeyExists(shiftAttrName) ? definition[shiftAttrName].AsBool(false) : null
            );

            mClientApi = api as ICoreClientAPI;
        }

        public KeyEventType GetEventType()
        {
            return mType;
        }
        public bool CheckIfShouldBeHandled(KeyEvent keyEvent, KeyEventType eventType)
        {
            if (mClientApi == null) throw new InvalidOperationException("BasicKey.CheckIfShouldBeHandled() called on server side");

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
                HotKeyCodes = mModifiers.GetCodes().AsEnumerable().Append(mHotkey).ToArray()
            };
        }
    }
}
