using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Inputs;

public class HotkeyInput : BaseInput, IHotkeyInput
{
    private readonly string[] mHotkeys;

    public HotkeyInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mHotkeys = definition["hotkeys"].AsArray().Select(value => value.AsString()).ToArray();

        if (mApi is not ICoreClientAPI clientApi)
        {
            //LogError($"HotkeyInput is client side input, but was instantiated on server side");
            return;
        }

        bool missingHotkey = false;
        foreach (string hotkey in mHotkeys.Where(value => !clientApi.Input.HotKeys.ContainsKey(value)))
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

    public string[] Hotkeys => mHotkeys;

    public override string ToString() => $"{Utils.GetTypeName(GetType())}:{mHotkeys.Aggregate((x,y) => $"{x}, {y}")}";
}
