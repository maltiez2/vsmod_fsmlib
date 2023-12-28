using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IKeyInput;
using MaltiezFSM.API;
using static MaltiezFSM.API.IMouseInput;
using Vintagestory.API.Client;
using System;
using System.Linq;

namespace MaltiezFSM.Inputs
{
    public class BasicMouse : BaseInput, IMouseInput
    {
        public const string keyAttrName = "key";
        public const string keyPressTypeAttrName = "type";
        public const string altAttrName = "alt";
        public const string ctrlAttrName = "ctrl";
        public const string shiftAttrName = "shift";
        public const string repeatAttrName = "repeat";
        public const string nameAttrName = "name";

        private string mKey;
        private bool mRepeatable;
        private EnumMouseButton mKeyEnum;
        private MouseEventType mType;
        private KeyPressModifiers mModifiers;
        private ICoreClientAPI mClientApi;
        private string mLangName;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mKey = definition[keyAttrName].AsString();
            mLangName = definition[nameAttrName].AsString();
            mRepeatable = definition[repeatAttrName].AsBool(false);
            mKeyEnum = (EnumMouseButton)Enum.Parse(typeof(EnumMouseButton), mKey);
            switch (definition[keyPressTypeAttrName].AsString())
            {
                case ("released"):
                    mType = MouseEventType.MouseUp;
                    break;
                case ("pressed"):
                    mType = MouseEventType.MouseDown;
                    break;
                default:
                    mType = MouseEventType.MouseDown;
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

        public MouseEventType GetEventType()
        {
            return mType;
        }
        public bool CheckIfShouldBeHandled(MouseEvent mouseEvent, MouseEventType eventType)
        {
            if (mClientApi == null) throw new InvalidOperationException("BasicMouse.CheckIfShouldBeHandled() called on server side");

            if (mType != eventType) return false;
            if (mouseEvent.Button != mKeyEnum) return false;
            if (!ClientCheckModifiers()) return false;

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

        public override WorldInteraction GetInteractionInfo(ItemSlot slot)
        {
            if (mLangName == null) return null;
            
            return new WorldInteraction()
            {
                ActionLangCode = mLangName,
                MouseButton = mKeyEnum,
                HotKeyCodes = mModifiers.Codes
            };
        }

        public bool IsRepeatable()
        {
            return mRepeatable;
        }

        protected bool ClientCheckModifiers()
        {        
            bool altPressed = mClientApi.Input.KeyboardKeyState[(int)GlKeys.AltLeft] || mClientApi.Input.KeyboardKeyState[(int)GlKeys.AltRight];
            bool ctrlPressed = mClientApi.Input.KeyboardKeyState[(int)GlKeys.ControlLeft] || mClientApi.Input.KeyboardKeyState[(int)GlKeys.ControlRight];
            bool shiftPressed = mClientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftLeft] || mClientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftRight];

            if (mModifiers.Alt != null && altPressed != mModifiers.Alt) return false;
            if (mModifiers.Ctrl != null && ctrlPressed != mModifiers.Ctrl) return false;
            if (mModifiers.Shift != null && shiftPressed != mModifiers.Shift) return false;

            return true;
        }

        public void SetModifiers(KeyPressModifiers modifiers)
        {
            mModifiers = modifiers;
        }
    }
}
