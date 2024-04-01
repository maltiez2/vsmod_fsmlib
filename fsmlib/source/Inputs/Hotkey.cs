using MaltiezFSM.API;
using MaltiezFSM.Framework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Inputs;

public class HotkeyInput : BaseInput, IHotkeyInput
{
    private readonly string[] _hotkeys;

    public HotkeyInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        _hotkeys = definition["hotkeys"].AsArray().Select(value => value.AsString()).ToArray();

        CheckHotkeys();
    }

    public HotkeyInput(ICoreAPI api, string code, CollectibleObject collectible, BaseInputProperties? baseProperties, params string[] hotkeys) : base(api, code, collectible, baseProperties)
    {
        _hotkeys = hotkeys;

        CheckHotkeys();
    }

    public HotkeyInput(ICoreAPI api, string code, CollectibleObject collectible, params string[] hotkeys) : base(api, code, collectible, null)
    {
        _hotkeys = hotkeys;

        CheckHotkeys();
    }

    public string[] Hotkeys => _hotkeys;

    public override string ToString() => $"{Utils.GetTypeName(GetType())}:{_hotkeys.Aggregate((x, y) => $"{x}, {y}")}";

    private void CheckHotkeys()
    {
        if (mApi is not ICoreClientAPI clientApi) return;

        bool missingHotkey = false;
        foreach (string hotkey in _hotkeys.Where(value => !clientApi.Input.HotKeys.ContainsKey(value)))
        {
            LogWarn($"Hotkey '{hotkey}' not found");
            LogVerbose($"Hotkey '{hotkey}' not found");
            missingHotkey = true;
        }
        if (missingHotkey)
        {
            LogVerbose($"Available hotkeys: {clientApi.Input.HotKeys.Select(x => x.Key).Aggregate((x, y) => $"{x}, {y}")}");
        }
    }
}
