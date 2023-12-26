using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using Vintagestory.API.Client;
using System.Linq;

namespace MaltiezFSM.Inputs
{
    public class BasicHotkey : BaseInput, IHotkeyInput
    {
        public const string keyAttrName = "key";
        public const string altAttrName = "alt";
        public const string ctrlAttrName = "ctrl";
        public const string shiftAttrName = "shift";
        public const string nameAttrName = "name";

        private string mKey;
        private string mCode;
        private KeyPressModifiers mModifiers;
        private string mLangName;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            mLangName = definition[nameAttrName].AsString(code);
            mKey = definition[keyAttrName].AsString(); // @TODO @LOCAL Add localization
            mCode = code;

            mModifiers = new KeyPressModifiers
            (
                definition.KeyExists(altAttrName) ? definition[altAttrName].AsBool(false) : null,
                definition.KeyExists(ctrlAttrName) ? definition[ctrlAttrName].AsBool(false) : null,
                definition.KeyExists(shiftAttrName) ? definition[shiftAttrName].AsBool(false) : null
            );
        }

        KeyPressModifiers IKeyRelatedInput.GetModifiers()
        {
            return mModifiers;
        }
        string IKeyRelatedInput.GetKey()
        {
            return mKey;
        }
        public override WorldInteraction GetInteractionInfo(ItemSlot slot)
        {
            return new WorldInteraction()
            {
                ActionLangCode = mLangName,
                MouseButton = EnumMouseButton.None,
                HotKeyCodes = mModifiers.GetCodes().AsEnumerable().Append(mCode).ToArray()
            };
        }

        string IHotkeyInput.GetLangName()
        {
            return mLangName;
        }

        public void SetModifiers(KeyPressModifiers modifiers)
        {
            mModifiers = modifiers;
        }
    }
}

