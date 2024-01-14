using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class ItemStackGiver : BaseSystem
{
    public ItemStackGiver(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        ItemStack? item;

        if (!parameters.KeyExists("stack"))
        {
            item = GenItemStack(parameters);
        }
        else
        {
            item = DeserializeItemStack(parameters["stack"]);
        }

        if (item == null) return false;

        return player.Entity.TryGiveItemStack(item);
    }

    private ItemStack? GenItemStack(JsonObject itemStackDefinition)
    {
        string? itemCode = itemStackDefinition["itemCode"].AsString();
        string? itemDomain = itemStackDefinition["domain"].AsString();
        string? path = itemStackDefinition["path"].AsString();
        EnumItemClass itemType = (EnumItemClass)Enum.Parse(typeof(EnumItemClass), itemStackDefinition["type"].AsString("Item"));
        int quantity = itemStackDefinition["quantity"].AsInt(1);
        JsonObject attributes = itemStackDefinition["attributes"];

        if (path == null && itemCode == null)
        {
            LogError($"No 'itemCode' in system request");
            return null;
        }

        if (path == null && itemDomain == null)
        {
            LogError($"No 'domain' in system request");
            return null;
        }

        JsonItemStack jsonItemStack = new()
        {
            Code = path != null ? new AssetLocation(path) : new AssetLocation(itemDomain, itemCode),
            Type = itemType,
            Quantity = quantity,
            Attributes = attributes
        };

        jsonItemStack.Resolve(mApi.World, "ItemStackGiver");

        return jsonItemStack.ResolvedItemstack;
    }

    public ItemStack? DeserializeItemStack(JsonObject itemStackDefinition)
    {
        JsonItemStack? jsonItemStack;

        try
        {
            jsonItemStack = itemStackDefinition.AsObject<JsonItemStack>();
        }
        catch (Exception exception)
        {
            LogError($"Unable to desirialize itemstack: '{itemStackDefinition}'");
            LogDebug($"Unable to desirialize itemstack: '{itemStackDefinition}'.\nException:\n{exception}");
            return null;
        }
        
        if (jsonItemStack?.Resolve(mApi.World, "fsmlib") == true)
        {
            return jsonItemStack.ResolvedItemstack;
        }
        else
        {
            LogError($"Unable to resolve itemstack: '{itemStackDefinition}'");
            return null;
        }
    }
}
