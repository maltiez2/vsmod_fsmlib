﻿using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class ChangeGroup : BaseSystem
{
    public ChangeGroup(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string group = parameters["group"].AsString();
        string value = parameters["value"].AsString();

        if (group == null)
        {
            LogError($"No 'group' in system request");
            return false;
        }
        if (value == null)
        {
            LogError($"No 'value' in system request");
            return false;
        }

        TryChangeVariant(slot.Itemstack, mApi, group, value);
        slot.MarkDirty();
        return true;
    }

    private static void TryChangeVariant(ItemStack stack, ICoreAPI api, string variantName, string variantValue, bool saveAttributes = true)
    {
        if (stack?.Collectible?.Variant?.ContainsKey(variantName) == null) return;

        ITreeAttribute clonedAttributes = stack.Attributes.Clone();
        int size = stack.StackSize;
        ItemStack newStack = new();

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
        newStack.StackSize = size;

        stack.SetFrom(newStack);
    }
}
