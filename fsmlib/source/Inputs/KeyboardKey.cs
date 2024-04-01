using MaltiezFSM.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.API.IKeyInput;

namespace MaltiezFSM.Inputs;

public struct KeyboardKeyInputProperties
{
    public string? HotKey { get; set; } = null;
    public string? Name { get; set; } = null;
    public KeyEventType EventType { get; set; } = KeyEventType.KeyDown;
    public KeyPressModifiers? Modifiers { get; set; } = null;

    public KeyboardKeyInputProperties()
    {
    }
}

public sealed class KeyboardKey : BaseInput, IKeyInput
{
    public KeyEventType EventType { get; private set; }
    public string HotKey { get; private set; }
    public string Name { get; private set; }
    public KeyPressModifiers Modifiers { get; set; }
    public GlKeys Key { get; set; }


    public KeyboardKey(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        HotKey = definition["hotkey"].AsString();
        Name = definition["name"].AsString(code);
        Key = (GlKeys)Enum.Parse(typeof(GlKeys), definition["key"].AsString());
        EventType = definition["type"].AsString() switch
        {
            "released" => KeyEventType.KeyUp,
            "pressed" => KeyEventType.KeyDown,
            "hold" => KeyEventType.KeyHold,
            _ => KeyEventType.KeyDown,
        };
        Modifiers = new KeyPressModifiers
        (
            definition.KeyExists("alt") ? definition["alt"].AsBool(false) : null,
            definition.KeyExists("ctrl") ? definition["ctrl"].AsBool(false) : null,
            definition.KeyExists("shift") ? definition["shift"].AsBool(false) : null
        );
    }

    public KeyboardKey(ICoreAPI api, string code, CollectibleObject collectible, GlKeys key, KeyboardKeyInputProperties? properties = null, BaseInputProperties? baseProperties = null) : base(api, code, collectible, baseProperties)
    {
        HotKey = properties?.HotKey ?? "";
        Name = properties?.Name ?? code;
        Key = key;
        EventType = properties?.EventType ?? KeyEventType.KeyDown;
        Modifiers = properties?.Modifiers ?? new(null, null, null);
    }

    public bool CheckIfShouldBeHandled(KeyEvent keyEvent, KeyEventType eventType)
    {
        if (EventType != eventType) return false;
        if (keyEvent.KeyCode != (int)Key && keyEvent.KeyCode2 != (int)Key) return false;
        if (Modifiers.Alt != null && keyEvent.AltPressed != Modifiers.Alt) return false;
        if (Modifiers.Ctrl != null && keyEvent.CtrlPressed != Modifiers.Ctrl) return false;
        if (Modifiers.Shift != null && keyEvent.ShiftPressed != Modifiers.Shift) return false;

        return true;
    }

    public override WorldInteraction GetInteractionInfo(ItemSlot slot)
    {
        return new WorldInteraction()
        {
            ActionLangCode = Name,
            MouseButton = EnumMouseButton.None,
            HotKeyCodes = Modifiers.Codes.Append(HotKey).ToArray()
        };
    }

    public override string ToString() => $"{EventType}: {Key}, {Modifiers}";
}
