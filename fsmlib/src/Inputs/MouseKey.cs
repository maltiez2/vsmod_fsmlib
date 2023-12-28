﻿using MaltiezFSM.API;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IMouseInput;

#nullable enable

namespace MaltiezFSM.Inputs
{
    public class MouseKey : BaseInput, IMouseInput
    {
        public MouseEventType EventType { get; private set; }
        public KeyPressModifiers Modifiers { get; set; }
        public EnumMouseButton Key { get; set; }
        public string Name { get; private set; }

        public MouseKey(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
        {
            Name = definition["name"].AsString("");
            Key = (EnumMouseButton)Enum.Parse(typeof(EnumMouseButton), definition["key"].AsString());
            EventType = definition["type"].AsString() switch
            {
                ("released") => MouseEventType.MouseUp,
                ("pressed") => MouseEventType.MouseDown,
                _ => MouseEventType.MouseDown,
            };
            Modifiers = new KeyPressModifiers
            (
                definition.KeyExists("alt") ? definition["alt"].AsBool(false) : null,
                definition.KeyExists("ctrl") ? definition["ctrl"].AsBool(false) : null,
                definition.KeyExists("shift") ? definition["shift"].AsBool(false) : null
            );
        }

        public bool CheckIfShouldBeHandled(MouseEvent mouseEvent, MouseEventType eventType)
        {
            if (EventType != eventType) return false;
            if (mouseEvent.Button != Key) return false;
            if (!ClientCheckModifiers()) return false;

            return true;
        }

        public override WorldInteraction? GetInteractionInfo(ItemSlot slot)
        {
            if (Name == "") return null;

            return new WorldInteraction()
            {
                ActionLangCode = Name,
                MouseButton = Key,
                HotKeyCodes = Modifiers.Codes.ToArray()
            };
        }

        protected bool ClientCheckModifiers()
        {
            if (mApi is not ICoreClientAPI clientApi) return false;

            bool altPressed = clientApi.Input.KeyboardKeyState[(int)GlKeys.AltLeft] || clientApi.Input.KeyboardKeyState[(int)GlKeys.AltRight];
            bool ctrlPressed = clientApi.Input.KeyboardKeyState[(int)GlKeys.ControlLeft] || clientApi.Input.KeyboardKeyState[(int)GlKeys.ControlRight];
            bool shiftPressed = clientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftLeft] || clientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftRight];

            if (Modifiers.Alt != null && altPressed != Modifiers.Alt) return false;
            if (Modifiers.Ctrl != null && ctrlPressed != Modifiers.Ctrl) return false;
            if (Modifiers.Shift != null && shiftPressed != Modifiers.Shift) return false;

            return true;
        }
    }
}
