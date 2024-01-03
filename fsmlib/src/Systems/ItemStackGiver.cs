using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Systems
{
    public class ItemStackGiver : BaseSystem
    {
        public ItemStackGiver(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
        {
        }

        public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            ItemStack? item = GenItemStack(parameters);

            if (item == null) return false;

            return player.Entity.TryGiveItemStack(item);
        }

        private ItemStack? GenItemStack(JsonObject itemStackDefinition)
        {
            string itemCode = itemStackDefinition["code"].AsString();
            string itemDomain = itemStackDefinition["domain"].AsString();
            EnumItemClass itemType = (EnumItemClass)Enum.Parse(typeof(EnumItemClass), itemStackDefinition["type"].AsString("Item"));
            int quantity = itemStackDefinition["quantity"].AsInt(1);
            JsonObject attributes = itemStackDefinition["attributes"];

            JsonItemStack jsonItemStack = new()
            {
                Code = new AssetLocation(itemDomain, itemCode),
                Type = itemType,
                Quantity = quantity,
                Attributes = attributes
            };

            jsonItemStack.Resolve(mApi.World, "ItemStackGiver");

            return jsonItemStack.ResolvedItemstack;
        }
    }
}
