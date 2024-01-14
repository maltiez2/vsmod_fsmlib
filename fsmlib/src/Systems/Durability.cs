using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public sealed class Durability : BaseSystem
{
    public Durability(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    public override bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Verify(slot, player, parameters)) return false;

        bool require = parameters["require"].AsBool(false);

        if (!require) return true;

        int durability = slot?.Itemstack?.Collectible?.GetRemainingDurability(slot.Itemstack) ?? 0;
        int maxDurability = slot?.Itemstack?.Collectible?.GetMaxDurability(slot.Itemstack) ?? 0;
        int amount = parameters["amount"].AsInt(maxDurability);

        return amount > 0 ? durability >= amount : (maxDurability - durability) >= Math.Abs(amount);
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("check");

        switch (action)
        {
            case "change":
                ChangeDurability(slot, player, parameters);
                break;
            case "check":
                break;
            default:
                LogActions(action, "change", "check");
                return false;
        }

        return true;
    }

    private static void ChangeDurability(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        int amount = parameters["amount"].AsInt(0);
        bool destroy = parameters["destroy"].AsBool(false);

        if (destroy && amount < 0)
        {
            slot.Itemstack.Collectible.DamageItem(player.Entity.Api.World, player.Entity, slot, Math.Abs(amount));
        }
        else
        {
            int durability = slot.Itemstack?.Collectible?.GetRemainingDurability(slot.Itemstack) ?? 0;
            int maxDurability = slot.Itemstack?.Collectible?.GetMaxDurability(slot.Itemstack) ?? 0;

            slot.Itemstack?.Attributes.SetInt("durability", Math.Clamp(durability + amount, 0, maxDurability));
        }
    }
}
