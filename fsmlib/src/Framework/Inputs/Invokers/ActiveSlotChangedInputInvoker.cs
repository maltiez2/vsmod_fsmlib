using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework;

public sealed class ActiveSlotChangedInputInvoker : IInputInvoker
{
    private readonly ICoreServerAPI? mServerApi;
    private readonly ICoreClientAPI? mClientApi;
    private readonly List<ISlotChangedAfter> mInputsAfter = new();
    private readonly List<ISlotChangedBefore> mInputsBefore = new();
    private readonly Dictionary<IInput, IInputInvoker.InputCallback> mInputs = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private bool mDisposed = false;

    public ActiveSlotChangedInputInvoker(ICoreAPI api)
    {
        if (api is ICoreServerAPI serverApi)
        {
            mServerApi = serverApi;
            mServerApi.Event.AfterActiveSlotChanged += ActiveSlotChangeAfterServer;
            mServerApi.Event.BeforeActiveSlotChanged += ActiveSlotChangeBeforeServer;
        }
        else if (api is ICoreClientAPI clientApi)
        {
            mClientApi = clientApi;
            mClientApi.Event.AfterActiveSlotChanged += ActiveSlotChangeAfterClient;
            mClientApi.Event.BeforeActiveSlotChanged += ActiveSlotChangeBeforeClient;
        }
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

    private void ActiveSlotChangeAfterServer(IServerPlayer player, ActiveSlotChangeEventArgs eventData)
    {
        if (player == null) return;
        
        ItemSlot from = GetSlot(player, eventData.FromSlot);
        ItemSlot to = GetSlot(player, eventData.ToSlot);

        foreach (ISlotChangedAfter input in mInputsAfter)
        {
            switch (input.EventType)
            {
                case ISlotInput.SlotEventType.FromSlot:
                    _ = HandleInput(player, from, input);
                    break;
                case ISlotInput.SlotEventType.ToSlot:
                    _ = HandleInput(player, to, input);
                    break;
            }
        }
    }
    private void ActiveSlotChangeAfterClient(ActiveSlotChangeEventArgs eventData)
    {
        if (mClientApi?.World?.Player == null) return;

        ItemSlot from = GetSlot(eventData.FromSlot, mClientApi);
        ItemSlot to = GetSlot(eventData.ToSlot, mClientApi);

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

    private EnumHandling ActiveSlotChangeBeforeServer(IServerPlayer player, ActiveSlotChangeEventArgs eventData)
    {
        if (player == null) return EnumHandling.PassThrough;

        ItemSlot from = GetSlot(player, eventData.FromSlot);
        ItemSlot to = GetSlot(player, eventData.ToSlot);

        bool atLeastOneHandled = false;

        foreach (ISlotChangedBefore input in mInputsBefore)
        {
            EnumHandling handled = input.EventType switch
            {
                ISlotInput.SlotEventType.FromSlot => HandleInput(player, from, input),
                ISlotInput.SlotEventType.ToSlot => HandleInput(player, to, input),
                _ => EnumHandling.PassThrough,
            };

            if (handled == EnumHandling.Handled) atLeastOneHandled = true;
        }

        return atLeastOneHandled ? EnumHandling.Handled : EnumHandling.PassThrough;
    }
    private EnumHandling ActiveSlotChangeBeforeClient(ActiveSlotChangeEventArgs eventData)
    {
        if (mClientApi?.World?.Player == null) return EnumHandling.PassThrough;

        ItemSlot from = GetSlot(eventData.FromSlot, mClientApi);
        ItemSlot to = GetSlot(eventData.ToSlot, mClientApi);

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

    private static ItemSlot GetSlot(IServerPlayer player, int id)
    {
        return player.InventoryManager.GetHotbarInventory()[id];
    }
    private static ItemSlot GetSlot(int id, ICoreClientAPI api)
    {
        return api.World.Player.InventoryManager.GetHotbarInventory()[id];
    }

    private EnumHandling HandleInput(IServerPlayer player, ItemSlot slot, IInput input)
    {
        CollectibleObject? slotCollectible = slot?.Itemstack?.Collectible;
        if (slotCollectible != mCollectibles[input]) return EnumHandling.PassThrough;

        bool handled = mInputs[input].Invoke(new(input.Slot, slot, player), player, input, false);

        return handled ? (input as ISlotChangedBefore)?.Handling ?? EnumHandling.Handled : EnumHandling.PassThrough;
    }
    private EnumHandling HandleInput(ItemSlot slot, IInput input)
    {
        if (mClientApi == null) return EnumHandling.PassThrough;

        if (mClientApi.World?.Player?.Entity == null)
        {
            Logger.Debug(mClientApi, this, $"Player entity is null. Skipping invoking events.");
            return EnumHandling.PassThrough;
        }

        CollectibleObject? slotCollectible = slot?.Itemstack?.Collectible;
        if (slotCollectible != mCollectibles[input]) return EnumHandling.PassThrough;

        bool handled = mInputs[input].Invoke(new(input.Slot, slot, mClientApi.World.Player), mClientApi.World.Player, input, false);

        return handled ? (input as ISlotChangedBefore)?.Handling ?? EnumHandling.Handled : EnumHandling.PassThrough;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        if (mClientApi != null)
        {
            mClientApi.Event.AfterActiveSlotChanged -= ActiveSlotChangeAfterClient;
            mClientApi.Event.BeforeActiveSlotChanged -= ActiveSlotChangeBeforeClient;
        }
        else if (mServerApi != null)
        {
            mServerApi.Event.AfterActiveSlotChanged -= ActiveSlotChangeAfterServer;
            mServerApi.Event.BeforeActiveSlotChanged -= ActiveSlotChangeBeforeServer;
        }
    }
}