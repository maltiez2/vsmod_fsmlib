using HarmonyLib;
using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public sealed class SlotInputInvoker : IInputInvoker
{
    private bool mDisposed = false;
    private readonly ICoreAPI mApi;
    private readonly Dictionary<ISlotContentInput.SlotEventType, Dictionary<SlotType, Dictionary<ISlotContentInput, IInputInvoker.InputCallback>>> mInputs = new();

    public SlotInputInvoker(ICoreAPI api)
    {
        mApi = api;
        SlotInputInvokerIntegration.Patch(api.Side);
        SlotInputInvokerIntegration.Instances[api.Side] = this;
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is not ISlotContentInput contentInput) return;

        if (!mInputs.ContainsKey(contentInput.EventType)) mInputs.Add(contentInput.EventType, new());
        if (!mInputs[contentInput.EventType].ContainsKey(contentInput.Slot)) mInputs[contentInput.EventType].Add(contentInput.Slot, new());

        mInputs[contentInput.EventType][contentInput.Slot].Add(contentInput, callback);
    }

    public void ContentChangeHandler(ItemSlot slot, ISlotContentInput.SlotEventType eventType)
    {
        if (!mInputs.ContainsKey(eventType)) return;

        IPlayer? player = (slot.Inventory as InventoryBasePlayer)?.Player;

        if (player == null) return;

        foreach (Dictionary<ISlotContentInput, IInputInvoker.InputCallback> inputs in mInputs[eventType].Where(entry => SlotData.CheckSlotType(entry.Key, slot, player)).Select(entry => entry.Value))
        {
            InvokeInputs(inputs, slot, player);
        }
    }

    private static void InvokeInputs(Dictionary<ISlotContentInput, IInputInvoker.InputCallback> inputs, ItemSlot slot, IPlayer player)
    {
        CollectibleObject? collectible = slot.Itemstack?.Collectible;

        if (collectible == null) return;

        foreach ((ISlotContentInput input, IInputInvoker.InputCallback callback) in inputs.Where(entry => entry.Key.Collectible == collectible))
        {
            if (!input.CheckModifiers(player, null)) continue;

            if (callback.Invoke(new(input.Slot, slot, player), player, input, false))
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;

        SlotInputInvokerIntegration.Unpatch(mApi.Side);
    }
}

internal static class SlotInputInvokerIntegration
{
    public static readonly Dictionary<EnumAppSide, SlotInputInvoker> Instances = new();

    public static void Patch(EnumAppSide side)
    {
        if (side == EnumAppSide.Client)
        {
            new Harmony("fsmlib").Patch(
                    AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(TakeFromSlotClient)))
                    );
            new Harmony("fsmlib").Patch(
                        AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOutWhole)),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(TakeAllFromSlotClient)))
                        );
            new Harmony("fsmlib").Patch(
                        typeof(ItemSlot).GetMethod("FlipWith", AccessTools.all),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(FlipWithPreClient)))
                        );
            new Harmony("fsmlib").Patch(
                        AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.OnItemSlotModified)),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(OnItemSlotModifiedClient)))
                        );
        }

        if (side == EnumAppSide.Server)
        {
            new Harmony("fsmlib").Patch(
                    AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(TakeFromSlotServer)))
                    );
            new Harmony("fsmlib").Patch(
                        AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOutWhole)),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(TakeAllFromSlotServer)))
                        );
            new Harmony("fsmlib").Patch(
                        typeof(ItemSlot).GetMethod("FlipWith", AccessTools.all),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(FlipWithPreServer)))
                        );
            new Harmony("fsmlib").Patch(
                        AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.OnItemSlotModified)),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SlotInputInvokerIntegration), nameof(OnItemSlotModifiedServer)))
                        );
        }
    }
    public static void Unpatch(EnumAppSide side)
    {
        new Harmony("fsmlib").Unpatch(AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut)), HarmonyPatchType.Prefix, "fsmlib");
        new Harmony("fsmlib").Unpatch(AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOutWhole)), HarmonyPatchType.Prefix, "fsmlib");
        new Harmony("fsmlib").Unpatch(typeof(ItemSlot).GetMethod("FlipWith", AccessTools.all), HarmonyPatchType.Prefix, "fsmlib");
        new Harmony("fsmlib").Unpatch(AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.OnItemSlotModified)), HarmonyPatchType.Prefix, "fsmlib");
    }

    private static void TakeFromSlotClient(ItemSlot __instance, int quantity) => TakeFromSlot(__instance, quantity, EnumAppSide.Client);
    private static void TakeFromSlotServer(ItemSlot __instance, int quantity) => TakeFromSlot(__instance, quantity, EnumAppSide.Server);
    private static void TakeAllFromSlotClient(ItemSlot __instance) => TakeAllFromSlot(__instance, EnumAppSide.Client);
    private static void TakeAllFromSlotServer(ItemSlot __instance) => TakeAllFromSlot(__instance, EnumAppSide.Server);
    private static void FlipWithPreClient(ItemSlot __instance, ItemSlot withSlot) => FlipWithPre(__instance, withSlot, EnumAppSide.Client);
    private static void FlipWithPreServer(ItemSlot __instance, ItemSlot withSlot) => FlipWithPre(__instance, withSlot, EnumAppSide.Server);
    private static void OnItemSlotModifiedClient(ItemSlot __instance) => OnItemSlotModified(__instance, EnumAppSide.Client);
    private static void OnItemSlotModifiedServer(ItemSlot __instance) => OnItemSlotModified(__instance, EnumAppSide.Server);

    private static void TakeFromSlot(ItemSlot __instance, int quantity, EnumAppSide side)
    {
        if (__instance.Itemstack == null) return;

        if (quantity >= __instance.Itemstack.StackSize) return;

        Instances[side]?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.SomeTaken);
    }
    private static void TakeAllFromSlot(ItemSlot __instance, EnumAppSide side)
    {
        if (__instance.Itemstack == null) return;

        Instances[side]?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.AllTaken);
    }
    private static void FlipWithPre(ItemSlot __instance, ItemSlot withSlot, EnumAppSide side)
    {
        if (withSlot.StackSize <= __instance.MaxSlotStackSize)
        {
            Instances[side]?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.AllTaken);
            Instances[side]?.ContentChangeHandler(withSlot, ISlotContentInput.SlotEventType.AllTaken);
        }
    }
    private static void OnItemSlotModified(ItemSlot __instance, EnumAppSide side)
    {
        Instances[side]?.ContentChangeHandler(__instance, ISlotContentInput.SlotEventType.AfterModified);
    }
}
