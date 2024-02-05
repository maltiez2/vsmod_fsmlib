using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace MaltiezFSM.Framework;

public enum SlotType
{
    MainHand,
    OffHand,
    Inventory,
    HotBar,
    Character,
    Backpack,
    Crafting
}

public class SlotTypeException : System.Exception
{
    public SlotTypeException(string message) : base(message)
    {

    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct SlotData
{
    public SlotType SlotType { get; set; }
    public int SlotId { get; set; } = -1;
    public string InventoryId { get; set; } = "";

    public SlotData(SlotType type)
    {
        switch (type)
        {
            case SlotType.HotBar:
            case SlotType.Inventory:
            case SlotType.Character:
            case SlotType.Backpack:
            case SlotType.Crafting:
                throw new SlotTypeException($"'{type}' requires for slot and player to be specified");
            default:
                SlotType = type;
                break;
        }
    }
    public SlotData(SlotType type, ItemSlot slot, IPlayer player)
    {
        SlotType = type;

        switch (type)
        {
            case SlotType.HotBar:
                SlotId = player.InventoryManager.GetHotbarInventory().GetSlotId(slot);
                break;
            case SlotType.Inventory:
                {
                    (string inventoryId, int slotId) = GetSlotIdFromInventory(slot, player) ?? ("", -1);
                    SlotId = slotId;
                    InventoryId = inventoryId;
                }
                break;
            case SlotType.Character:
                {
                    (string inventoryId, int slotId) = GetSlotIdFromInventory(slot, player, GlobalConstants.characterInvClassName) ?? ("", -1);
                    SlotId = slotId;
                    InventoryId = inventoryId;
                }
                break;
            case SlotType.Backpack:
                {
                    (string inventoryId, int slotId) = GetSlotIdFromInventory(slot, player, GlobalConstants.backpackInvClassName) ?? ("", -1);
                    SlotId = slotId;
                    InventoryId = inventoryId;
                }
                break;
            case SlotType.Crafting:
                {
                    (string inventoryId, int slotId) = GetSlotIdFromInventory(slot, player, GlobalConstants.craftingInvClassName) ?? ("", -1);
                    SlotId = slotId;
                    InventoryId = inventoryId;
                }
                break;
            default: break;
        }
    }

    public static SlotData? Construct(SlotType type, ItemSlot? slot, IPlayer? player)
    {
        if (slot != null && player != null) return new(type, slot, player);

        return type switch
        {
            SlotType.HotBar or SlotType.Inventory or SlotType.Character or SlotType.Backpack or SlotType.Crafting => null,
            _ => new(type),
        };
    }

    public static bool CheckSlotType(SlotType slotType, ItemSlot slot, IPlayer player)
    {
        return slotType switch
        {
            SlotType.HotBar => player.InventoryManager.GetHotbarInventory().GetSlotId(slot) != -1,
            SlotType.MainHand => player.Entity.RightHandItemSlot == slot,
            SlotType.OffHand => player.Entity.LeftHandItemSlot == slot,
            SlotType.Inventory => GetAllOwnInventories(player).Select(entry => entry.GetSlotId(slot) != -1).Aggregate((x, y) => x && y),
            SlotType.Character => player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName).GetSlotId(slot) != -1,
            SlotType.Backpack => player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName).GetSlotId(slot) != -1,
            SlotType.Crafting => player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName).GetSlotId(slot) != -1,
            _ => false,
        };
    }

    public readonly ItemSlot? Slot(IPlayer player)
    {
        return SlotType switch
        {
            SlotType.HotBar => player.InventoryManager.GetHotbarInventory()[SlotId],
            SlotType.MainHand => player.Entity.RightHandItemSlot,
            SlotType.OffHand => player.Entity.LeftHandItemSlot,
            SlotType.Inventory => player.InventoryManager.GetInventory(InventoryId)[SlotId],
            SlotType.Character => player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName)[SlotId],
            SlotType.Backpack => player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName)[SlotId],
            SlotType.Crafting => player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName)[SlotId],
            _ => null,
        };
    }

    public static IEnumerable<SlotData> GetForAllSlots(SlotType type, CollectibleObject collectible, IPlayer? player = null)
    {
        HashSet<SlotData> slots = new();

        switch (type)
        {
            case SlotType.Inventory:
                if (player == null) break;
                foreach (ItemSlot hotbarSlot in GetOwnSlots(player, collectible))
                {
                    slots.Add(new(type, hotbarSlot, player));
                }
                break;
            case SlotType.Character:
            case SlotType.Backpack:
            case SlotType.HotBar:
            case SlotType.Crafting:
                if (player == null) break;
                foreach (ItemSlot hotbarSlot in GetOwnSlots(player, InventoriesIds[type], collectible))
                {
                    slots.Add(new(type, hotbarSlot, player));
                }
                break;
            case SlotType.MainHand:
                if (player?.Entity?.RightHandItemSlot?.Itemstack?.Collectible == collectible) slots.Add(new(type));
                break;
            case SlotType.OffHand:
                if (player?.Entity?.LeftHandItemSlot?.Itemstack?.Collectible == collectible) slots.Add(new(type));
                break;
            default:
                slots.Add(new(type));
                break;
        }

        return slots;
    }

    private static readonly Dictionary<SlotType, string> InventoriesIds = new Dictionary<SlotType, string>()
    {
        { SlotType.HotBar, GlobalConstants.hotBarInvClassName },
        { SlotType.Character, GlobalConstants.characterInvClassName },
        { SlotType.Backpack, GlobalConstants.backpackInvClassName },
        { SlotType.Crafting, GlobalConstants.craftingInvClassName }
    };

    private static (string inventory, int slot)? GetSlotIdFromInventory(ItemSlot slot, IPlayer player)
    {
        foreach (IInventory inventory in GetAllOwnInventories(player))
        {
            int slotId = inventory.GetSlotId(slot);
            if (slotId != -1)
            {
                return (inventory.InventoryID, slotId);
            }
        }

        return null;
    }
    private static (string inventory, int slot)? GetSlotIdFromInventory(ItemSlot slot, IPlayer player, string id)
    {
        IInventory? inventory = GetOwnInventory(player, id);
        
        if (inventory == null) return null;

        int slotId = inventory.GetSlotId(slot);
        
        if (slotId == -1) return null;

        return (inventory.InventoryID, slotId);
    }

    public override string ToString()
    {
        StringBuilder result = new();
        result.Append(SlotType);
        if (InventoryId != "") result.Append($" : {InventoryId}");
        if (SlotId != -1) result.Append($" : {SlotId}");
        return result.ToString();
    }

    private static bool FilterInventories(IInventory inventory, IPlayer player) => player.InventoryManager.GetOwnInventory(GlobalConstants.creativeInvClassName).InventoryID != inventory.InventoryID;
    private static IEnumerable<IInventory> GetAllOwnInventories(IPlayer player) => player.InventoryManager.Inventories.Where(entry => FilterInventories(entry.Value, player)).Select(entry => entry.Value);
    private static IInventory? GetOwnInventory(IPlayer player, string id) => FilterInventories(player.InventoryManager.GetInventory(id), player) ? player.InventoryManager.GetInventory(id) : null;
    private static IEnumerable<ItemSlot> GetOwnSlots(IPlayer player, string id, CollectibleObject collectible) => GetOwnInventory(player, id)?.Where(slot => slot?.Itemstack?.Collectible == collectible) ?? Enumerable.Empty<ItemSlot>();
    private static IEnumerable<ItemSlot> GetOwnSlots(IPlayer player, CollectibleObject collectible) => GetAllOwnInventories(player).SelectMany(entry => entry).Where(slot => slot?.Itemstack?.Collectible == collectible);
}