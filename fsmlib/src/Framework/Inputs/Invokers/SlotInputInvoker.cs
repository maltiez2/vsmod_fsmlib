using HarmonyLib;
using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework;

public sealed class SlotInputInvoker : IInputInvoker
{
    private static SlotInputInvoker? sInstance;

    private bool mDisposed = false;
    private ICoreServerAPI mApi;
    private readonly Dictionary<ISlotContentInput.SlotEventType, Dictionary<ISlotContentInput, IInputInvoker.InputCallback>> mInputs = new();

    public SlotInputInvoker(ICoreServerAPI api)
    {
        mApi = api;
        Patch();
        sInstance = this;
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is not ISlotContentInput contentInput) return;

        if (!mInputs.ContainsKey(contentInput.EventType)) mInputs.Add(contentInput.EventType, new());

        mInputs[contentInput.EventType].Add(contentInput, callback);
    }

    private void Patch()
    {
        new Harmony("fsmlib").Patch(
                    AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvoker), nameof(TakeFromSlot)))
                    );
        new Harmony("fsmlib").Patch(
                    AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOutWhole)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvoker), nameof(TakeAllFromSlot)))
                    );
    }
    private void Unpatch()
    {
        new Harmony("fsmlib").Unpatch(AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut)), HarmonyPatchType.Prefix, "fsmlib");
        new Harmony("fsmlib").Unpatch(AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOutWhole)), HarmonyPatchType.Prefix, "fsmlib");
    }
    private static void TakeFromSlot(ItemSlot __instance, int quantity)
    {
        if (__instance.Itemstack == null) return;

        if (quantity >= __instance.Itemstack.StackSize)
        {
            sInstance?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.AllTaken);
            return;
        }

        sInstance?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.SomeTaken);
    }
    private static void TakeAllFromSlot(ItemSlot __instance)
    {
        if (__instance.Itemstack == null) return;

        sInstance?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.AllTaken);
    }

    private void ContentChangeHandler(ItemSlot slot, ISlotContentInput.SlotEventType eventType)
    {
        IPlayer? player = (slot.Inventory as InventoryBasePlayer)?.Player;

        if (player == null) return;

        foreach ((ISlotContentInput input, IInputInvoker.InputCallback callback) in mInputs[eventType])
        {
            if (callback.Invoke(new(input.Slot, slot, player), player, input))
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;

        Unpatch();
    }
}
