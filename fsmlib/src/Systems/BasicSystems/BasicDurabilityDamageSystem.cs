using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    public class BasicDurabilityDamage : BaseSystem
    {
        public override bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Verify(slot, player, parameters)) return false;

            return slot?.Itemstack?.Collectible != null && player != null && mApi?.World != null;
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            int damage = parameters["value"].AsInt(1);
            slot?.Itemstack?.Collectible.DamageItem(mApi.World, player, slot, damage);
            return true;
        }
    }
}
