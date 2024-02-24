using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public sealed class ToolModeInputInvoker : IInputInvoker, IToolModeInvoker
{
    private readonly Dictionary<IToolModeInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IToolModeInput, CollectibleObject> mCollectibles = new();

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is not IToolModeInput toolModeInput) return;

        mCallbacks.Add(toolModeInput, callback);
        mCollectibles.Add(toolModeInput, collectible);
    }

    public void Invoke(ItemSlot slot, IPlayer player, string id)
    {
        foreach ((IToolModeInput? input, IInputInvoker.InputCallback? callback) in mCallbacks
            .Where(entry => mCollectibles[entry.Key] == slot?.Itemstack.Collectible)
            .Where(entry => entry.Key.ModeId == id))
        {
            if (!input.CheckModifiers(player, null)) continue;
            if (callback.Invoke(new SlotData(input.Slot, slot, player), player, input, synchronize: false)) return;
        }
    }

    public void Dispose()
    {
        // nothing to dispose
    }
}