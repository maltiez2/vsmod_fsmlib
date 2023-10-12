using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    internal class BasicDurabilityDamage : UniqueIdFactoryObject, ISystem
    {
        private ICoreAPI mApi;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mApi = api;
        }

        void ISystem.SetSystems(Dictionary<string, ISystem> systems)
        {
        }

        bool ISystem.Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            return slot?.Itemstack?.Collectible != null && player != null && mApi?.World != null;
        }

        bool ISystem.Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            int damage = parameters["value"].AsInt(1);
            slot?.Itemstack?.Collectible.DamageItem(mApi.World, player, slot, damage);
            return true;
        }
    }
}
