using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public sealed class ActiveSlotChangedInputInvoker : IInputInvoker
{
    private readonly ICoreClientAPI mClientApi;
    private readonly List<ISlotChangedAfter> mInputsAfter = new();
    private readonly List<ISlotChangedBefore> mInputsBefore = new();
    private readonly Dictionary<IInput, IInputInvoker.InputCallback> mInputs = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private bool mDisposed = false;

    public ActiveSlotChangedInputInvoker(ICoreClientAPI api)
    {
        mClientApi = api;

        mClientApi.Event.AfterActiveSlotChanged += ActiveSlotChangeAfter;
        mClientApi.Event.BeforeActiveSlotChanged += ActiveSlotChangeBefore;
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is ISlotChangedAfter slotAfter)
        {
            mInputs.Add(slotAfter, callback);
            mInputsAfter.Add(slotAfter);
            mCollectibles.Add(slotAfter, collectible);
        }
        else if (input is ISlotChangedBefore slotBefore)
        {
            mInputs.Add(slotBefore, callback);
            mInputsBefore.Add(slotBefore);
            mCollectibles.Add(slotBefore, collectible);
        }
    }

    private void ActiveSlotChangeAfter(ActiveSlotChangeEventArgs eventData)
    {
        ItemSlot from = GetSlot(eventData.FromSlot);
        ItemSlot to = GetSlot(eventData.ToSlot);

        foreach (ISlotChangedAfter input in mInputsAfter)
        {
            switch (input.EventType)
            {
                case ISlotInput.SlotEventType.FromSlot:
                    _ = HandleInput(from, input);
                    break;
                case ISlotInput.SlotEventType.ToSlot:
                    _ = HandleInput(to, input);
                    break;
            }
        }
    }

    private EnumHandling ActiveSlotChangeBefore(ActiveSlotChangeEventArgs eventData)
    {
        ItemSlot from = GetSlot(eventData.FromSlot);
        ItemSlot to = GetSlot(eventData.ToSlot);

        bool atLeastOneHandled = false;

        foreach (ISlotChangedBefore input in mInputsBefore)
        {
            EnumHandling handled = input.EventType switch
            {
                ISlotInput.SlotEventType.FromSlot => HandleInput(from, input),
                ISlotInput.SlotEventType.ToSlot => HandleInput(to, input),
                _ => EnumHandling.PassThrough,
            };

            if (handled == EnumHandling.Handled) atLeastOneHandled = true;
        }

        return atLeastOneHandled ? EnumHandling.Handled : EnumHandling.PassThrough;
    }

    private ItemSlot GetSlot(int id)
    {
        return mClientApi.World.Player.InventoryManager.GetHotbarInventory()[id];
    }

    private EnumHandling HandleInput(ItemSlot slot, IInput input)
    {
        CollectibleObject? slotCollectible = slot?.Itemstack?.Collectible;
        if (slotCollectible != mCollectibles[input]) return EnumHandling.PassThrough;

        bool handled = mInputs[input].Invoke(new(input.Slot, slot, mClientApi.World.Player), mClientApi.World.Player, input);

        return handled ? (input as ISlotChangedBefore)?.Handling ?? EnumHandling.Handled : EnumHandling.PassThrough;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mClientApi.Event.AfterActiveSlotChanged -= ActiveSlotChangeAfter;
        mClientApi.Event.BeforeActiveSlotChanged -= ActiveSlotChangeBefore;
    }
}
