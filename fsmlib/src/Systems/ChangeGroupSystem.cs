using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    internal class ChangeGroup : UniqueIdFactoryObject, ISystem
    {
        private ICoreAPI mApi;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mApi = api;
        }

        void ISystem.SetSystems(Dictionary<string, ISystem> systems)
        {
        }

        bool ISystem.Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            string group = parameters["group"].AsString();
            string value = parameters["value"].AsString();
            TryChangeVariant(slot.Itemstack, mApi, group, value);
            return true;
        }

        bool ISystem.Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            return true;
        }

        public void TryChangeVariant(ItemStack stack, ICoreAPI api, string variantName, string variantValue, bool saveAttributes = true) // Author: Dana (VS discord server)
        {
            if (stack?.Collectible?.Variant?.ContainsKey(variantName) == null) return;

            var clonedAttributes = stack.Attributes.Clone();

            var newStack = new ItemStack();

            switch (stack.Collectible.ItemClass)
            {
                case EnumItemClass.Block:
                    newStack = new ItemStack(api.World.GetBlock(stack.Collectible.CodeWithVariant(variantName, variantValue)));
                    break;

                case EnumItemClass.Item:
                    newStack = new ItemStack(api.World.GetItem(stack.Collectible.CodeWithVariant(variantName, variantValue)));
                    break;
            }

            if (saveAttributes) newStack.Attributes = clonedAttributes;

            stack.SetFrom(newStack);
        }
    }
}
