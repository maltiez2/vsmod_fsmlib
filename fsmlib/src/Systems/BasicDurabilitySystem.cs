using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    internal class BasicDurability : BaseSystem
    {
        public override bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Verify(slot, player, parameters)) return false;

            if (slot.Itemstack.Item == null) return false;
            
            int change = parameters["value"].AsInt(0);
            return CanChangeDurability(slot.Itemstack, change);
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            int change = parameters["value"].AsInt(0);
            if (!CanChangeDurability(slot.Itemstack, change)) return false;
            
            if (ChangeDurability(slot.Itemstack, change)) slot.MarkDirty();

            return true;
        }

        private bool CanChangeDurability(ItemStack itemstack, int amount)
        {
            if (amount >= 0 && itemstack.Collectible.GetRemainingDurability(itemstack) >= itemstack.Collectible.GetMaxDurability(itemstack))
            {
                return false;
            }

            int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack) + amount;
            remainingDurability = Math.Min(itemstack.Collectible.GetMaxDurability(itemstack), remainingDurability);

            if (remainingDurability < 0)
            {
                return false;
            }

            return true;
        }

        private bool ChangeDurability(ItemStack itemstack, int amount)
        {
            if (amount >= 0 && itemstack.Collectible.GetRemainingDurability(itemstack) >= itemstack.Collectible.GetMaxDurability(itemstack))
            {
                return false;
            }

            int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack) + amount;
            remainingDurability = Math.Min(itemstack.Collectible.GetMaxDurability(itemstack), remainingDurability);

            if (remainingDurability < 0)
            {
                return false;
            }

            itemstack.Attributes.SetInt("durability", Math.Max(remainingDurability, 0));

            return true;
        }
    }
}
