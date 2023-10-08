﻿using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

namespace MaltiezFSM.Inputs
{
    public class BasicHotkey : BaseInput, IHotkeyInput
    {
        public const string keyAttrName = "key";
        public const string keyPressTypeAttrName = "type";
        public const string altAttrName = "alt";
        public const string ctrlAttrName = "ctrl";
        public const string shiftAttrName = "shift";

        private string mKey;
        private KeyPressModifiers mModifiers;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mKey = definition[keyAttrName].AsString(); // @TODO @LOCAL Add localization
            mModifiers = new KeyPressModifiers
            (
                definition[altAttrName].AsBool(false),
                definition[ctrlAttrName].AsBool(false),
                definition[shiftAttrName].AsBool(false)
            );
        }

        public KeyPressModifiers GetIfAltCtrlShiftPressed()
        {
            return mModifiers;
        }
        public string GetKey()
        {
            return mKey;
        }
    }
}

