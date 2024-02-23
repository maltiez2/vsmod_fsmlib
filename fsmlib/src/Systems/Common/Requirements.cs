using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.Framework.Utils;



namespace MaltiezFSM.Systems.RequirementsApi;

public enum SearchMode
{
    Whitelist,
    Blacklist
}

public enum ProcessMode
{
    None,
    Durability,
    TakeAmount
}

public struct DurabilityMode
{
    public bool Destroy { get; set; }
    public bool Overflow { get; set; }
}

public interface IRequirement
{
    bool Verify(IPlayer player);

    IEnumerable<ItemSlot> Process(IPlayer player);
    IEnumerable<ItemSlot> Search(IPlayer player, bool findAll = false);
}

public class Requirement : IRequirement
{
    public SlotType Slot { get; set; }
    public SearchMode SearchMode { get; set; }
    public AssetLocation[] Locations { get; set; }
    public string? Name { get; set; }

    public Requirement(JsonObject definition)
    {
        Slot = (SlotType)Enum.Parse(typeof(SlotType), definition["slot"].AsString("Inventory"));
        SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), definition["search"].AsString("Whitelist"));
        Locations = definition.KeyExists("location") ? GetAssetLocations(definition["location"]) : Array.Empty<AssetLocation>();
        Name = definition["description"].AsString();
    }

    public virtual bool Verify(IPlayer player)
    {
        bool found = Search(Slot, player, CheckSlot, _ => true);
        return SearchMode switch
        {
            SearchMode.Blacklist => !found,
            SearchMode.Whitelist => found,
            _ => throw new NotImplementedException()
        };
    }
    public virtual IEnumerable<ItemSlot> Process(IPlayer player)
    {
        ItemStack? stack = null;
        Search(Slot, player, CheckSlot, (slot) => ProcessSlot(slot, out stack));
        return stack == null ? new List<ItemSlot>() : new List<ItemSlot>() { new DummySlot(stack) };
    }
    public virtual IEnumerable<ItemSlot> Search(IPlayer player, bool findAll = false)
    {
        List<ItemSlot> slots = new();
        Search(Slot, player, CheckSlot, (slot) => ProcessSlot(slot, slots));
        return slots;
    }

    protected bool CheckSlot(ItemSlot slot)
    {
        if (Locations.Length == 0) return slot.StackSize > 0;
        return slot?.Itemstack?.Collectible?.WildCardMatch(Locations) == true;
    }
    private static bool ProcessSlot(ItemSlot slot, out ItemStack? stack)
    {
        stack = slot.Itemstack;
        return true;
    }
    private static bool ProcessSlot(ItemSlot slot, List<ItemSlot> slots)
    {
        slots.Add(slot);
        return false;
    }

    private static AssetLocation[] GetAssetLocations(JsonObject definition)
    {
        if (definition.IsArray())
        {
            List<AssetLocation> locations = new();
            foreach (JsonObject location in definition.AsArray())
            {
                locations.Add(new(location.AsString()));
            }
            return locations.ToArray();
        }
        else
        {
            return new AssetLocation[] { new AssetLocation(definition.AsString()) };
        }
    }
    protected static bool Search(SlotType Slot, IPlayer player, System.Func<ItemSlot, bool> validator, System.Func<ItemSlot, bool> handler)
    {
        switch (Slot)
        {
            case SlotType.HotBar:

                foreach (ItemSlot hotbarSlot in player.InventoryManager.GetHotbarInventory().Where(validator))
                {
                    if (handler(hotbarSlot)) return true;
                }
                break;
            case SlotType.Inventory:
                foreach (IInventory inventory in SlotData.GetAllOwnInventories(player))
                {
                    foreach (ItemSlot inventorySlot in inventory.Where(validator))
                    {
                        if (handler(inventorySlot)) return true;
                    }
                }
                break;
            case SlotType.MainHand:
                ItemSlot mainHandSlot = player.Entity.RightHandItemSlot;
                if (validator(mainHandSlot) && handler(mainHandSlot)) return true;
                break;
            case SlotType.OffHand:
                ItemSlot offHandSlot = player.Entity.LeftHandItemSlot;
                if (validator(offHandSlot) && handler(offHandSlot)) return true;
                break;
            case SlotType.Character:
                foreach (ItemSlot characterSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName).Where(validator))
                {
                    if (handler(characterSlot)) return true;
                }
                break;
            case SlotType.Backpack:
                foreach (ItemSlot backpackSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName).Where(validator))
                {
                    if (handler(backpackSlot)) return true;
                }
                break;
            case SlotType.Crafting:
                foreach (ItemSlot craftingSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName).Where(validator))
                {
                    if (handler(craftingSlot)) return true;
                }
                break;
            default:
                break;
        }

        return false;
    }

    public static IRequirement Construct(JsonObject definition)
    {
        if (AmountRequirement.IsAmountRequirement(definition))
        {
            return new AmountRequirement(definition);
        }
        else if (DurabilityRequirement.IsDurabilityRequirement(definition))
        {
            return new DurabilityRequirement(definition);
        }

        return new Requirement(definition);
    }

    public override string? ToString()
    {
        return Name == null ? "" : Lang.Get(Name);
    }
}

public sealed class AmountRequirement : Requirement
{
    public int Amount { get; set; }

    public AmountRequirement(JsonObject definition) : base(definition)
    {
        Amount = definition["amount"]?.AsInt(1) ?? 1;
    }

    public override bool Verify(IPlayer player)
    {
        int amount = 0;
        bool found = Search(Slot, player, CheckSlot, (slot) => CountSlot(slot, ref amount));
        return SearchMode switch
        {
            SearchMode.Blacklist => !found,
            SearchMode.Whitelist => found,
            _ => throw new NotImplementedException()
        };
    }
    public override IEnumerable<ItemSlot> Process(IPlayer player)
    {
        int amount = 0;
        List<ItemSlot> slots = new();
        bool found = Search(Slot, player, CheckSlot, (slot) => CollectSlot(slot, ref amount, slots));
        return found ? CollectFromSlots(slots, Amount, player.Entity.World) : new List<ItemSlot>();
    }
    public override IEnumerable<ItemSlot> Search(IPlayer player, bool findAll = false)
    {
        int amount = 0;
        List<ItemSlot> slots = new();
        Search(Slot, player, CheckSlot, (slot) => CollectSlot(slot, ref amount, slots, all: true));
        return slots;
    }

    private bool CountSlot(ItemSlot slot, ref int amount)
    {
        amount += slot.StackSize;

        return amount >= Amount;
    }
    private bool CollectSlot(ItemSlot slot, ref int amount, List<ItemSlot> slots, bool all = false)
    {
        amount += slot.StackSize;

        for (int index = 1; index < slots.Count; index++)
        {
            if (slots[index].StackSize > slot.StackSize)
            {
                slots.Insert(index, slot);
                return amount >= Amount;
            }
        }

        slots.Add(slot);

        return !all && amount >= Amount;
    }
    public static IEnumerable<ItemSlot> CollectFromSlots(IEnumerable<ItemSlot> slots, int amount, IWorldAccessor world)
    {
        Dictionary<string, Stack<ItemSlot>> stacks = new();
        int takenTotal = 0;

        foreach (ItemSlot slot in slots)
        {
            string code = slot.Itemstack.Collectible.Code.ToString();

            if (!stacks.ContainsKey(code))
            {
                stacks.Add(code, new());
                stacks[code].Push(new DummySlot());
            }

            while (takenTotal < amount && slot.StackSize > 0)
            {
                int taken = slot.TryPutInto(world, stacks[code].Peek(), amount - takenTotal);
                takenTotal += taken;

                if (taken == 0 && slot.StackSize > 0 && takenTotal < amount)
                {
                    stacks[code].Push(new DummySlot());
                }
            }
        }

        return stacks.Values.SelectMany(slots => slots);
    }

    public static bool IsAmountRequirement(JsonObject definition) => definition.KeyExists("amount");
}

public sealed class DurabilityRequirement : Requirement
{
    public bool Destroy { get; set; }
    public bool Overflow { get; set; }
    public int Durability { get; set; }

    public DurabilityRequirement(JsonObject definition) : base(definition)
    {
        Destroy = definition["destroy"]?.AsBool(true) ?? true;
        Overflow = definition["overflow"]?.AsBool(false) ?? false;
        Durability = definition["durability"]?.AsInt(0) ?? 0;
    }

    public override bool Verify(IPlayer player)
    {
        int amount = 0;
        bool found = Search(Slot, player, CheckSlot, (slot) => CountSlot(slot, ref amount));
        return SearchMode switch
        {
            SearchMode.Blacklist => !found,
            SearchMode.Whitelist => found,
            _ => throw new NotImplementedException()
        };
    }
    public override IEnumerable<ItemSlot> Process(IPlayer player)
    {
        int amount = 0;
        List<ItemSlot> slots = new();
        bool found = Search(Slot, player, CheckSlot, (slot) => CollectSlot(slot, ref amount, slots));
        return found ? CollectFromSlots(slots, player, Durability, Destroy, Overflow) : new List<ItemSlot>();
    }
    public override IEnumerable<ItemSlot> Search(IPlayer player, bool findAll = false)
    {
        int amount = 0;
        List<ItemSlot> slots = new();
        Search(Slot, player, CheckSlot, (slot) => CollectSlot(slot, ref amount, slots, all: true));
        return slots;
    }

    private bool CountSlot(ItemSlot slot, ref int amount)
    {
        amount += slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);

        return Overflow || amount >= Math.Abs(Durability);
    }
    private bool CollectSlot(ItemSlot slot, ref int amount, List<ItemSlot> slots, bool all = false)
    {
        int durability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
        amount += durability;

        for (int index = 1; index < slots.Count; index++)
        {
            int slotDurability = slots[index].Itemstack.Collectible.GetRemainingDurability(slots[index].Itemstack);
            if (slotDurability > durability)
            {
                slots.Insert(index, slot);
                if (!all) return amount >= Durability;
            }
        }

        slots.Add(slot);

        return !all && amount >= Durability;
    }
    public static IEnumerable<ItemSlot> CollectFromSlots(IEnumerable<ItemSlot> slots, IPlayer player, int amount, bool destroy, bool overflow)
    {
        Dictionary<string, Stack<ItemSlot>> stacks = new();

        int amountTaken = 0;

        foreach (ItemSlot slot in slots)
        {
            string code = slot.Itemstack.Collectible.Code.ToString();
            ItemStack takenStack = slot.Itemstack.Clone();
            int durabilityTaken = TakeDurability(slot, amountTaken, player, amount, destroy);
            takenStack.Attributes.SetInt("durability", durabilityTaken);
            amountTaken += durabilityTaken;

            if (!stacks.ContainsKey(code))
            {
                stacks.Add(code, new());
            }

            stacks[code].Push(new DummySlot(takenStack));

            if (amountTaken != 0 && !overflow) break;
            if (Math.Abs(amountTaken) >= Math.Abs(amount)) break;
        }

        return stacks.Values.SelectMany(slots => slots);
    }
    public static int TakeDurability(ItemSlot slot, int amountTaken, IPlayer player, int amount, bool destroy)
    {
        int durability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
        int durabilityDamage = Math.Clamp(amount - amountTaken, 0, durability);

        if (destroy)
        {
            slot.Itemstack.Collectible.DamageItem(player.Entity.Api.World, player.Entity, slot, durabilityDamage);
        }
        else
        {
            slot.Itemstack.Attributes.SetInt("durability", durability - durabilityDamage);
        }

        return durabilityDamage;
    }

    public static bool IsDurabilityRequirement(JsonObject definition) => definition.KeyExists("durability");
}
