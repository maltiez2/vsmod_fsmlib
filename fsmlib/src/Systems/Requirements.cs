using MaltiezFSM.Systems.RequirementsApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;



namespace MaltiezFSM.Systems;

public class Requirements : BaseSystem, IItemStackHolder
{
    private readonly Dictionary<string, List<IRequirement>> mRequirements = new();
    private readonly Dictionary<string, string> mDescriptions = new();
    private readonly string mStackAttrName;

    public Requirements(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mStackAttrName = $"requirement.{code}";

        if (definition.Token is not JObject definitionObject) return;

        foreach ((string packCode, JToken? packToken) in definitionObject)
        {
            if (packCode == "class") continue;
            
            if (packToken is not JObject packObject) continue;
            JsonObject pack = new(packObject);

            if (pack.KeyExists("requirements") && pack["requirements"].IsArray())
            {
                mRequirements.Add(packCode, GetRequirements(pack["requirements"]));
            }
            else
            {
                mRequirements.Add(packCode, new() { Requirement.Construct(pack) });
            }

            mDescriptions.Add(packCode, pack["description"].AsString(""));
        }
    }
    private static List<IRequirement> GetRequirements(JsonObject requirements)
    {
        List<IRequirement> output = new();

        foreach (JsonObject definition in requirements.AsArray())
        {
            output.Add(Requirement.Construct(definition));
        }

        return output;
    }

    public override bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Verify(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("check");

        if (action == "empty") return Empty(slot);

        if (action != "take" && action != "check") return true;

        string? code = parameters["requirement"].AsString();

        if (code == null)
        {
            LogError($"No 'requirement' in system request");
            return false;
        }

        if (!mRequirements.ContainsKey(code))
        {
            LogError($"Requirement with code '{code}' not found.");
            return false;
        }

        bool fulfilled = true;
        foreach (IRequirement requirement in mRequirements[code])
        {
            if (!requirement.Verify(player))
            {
                fulfilled = false;
                SendMessage(player, requirement);
            }
        }

        return fulfilled;
    }
    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("check");

        switch (action)
        {
            case "empty":
            case "check":
                break;
            case "take":

                string? code = parameters["requirement"].AsString();
                if (code == null)
                {
                    LogError($"No 'requirement' in system request");
                    return false;
                }
                if (!mRequirements.ContainsKey(code))
                {
                    LogError($"Requirement with code '{code}' not found.");
                    return false;
                }

                Take(slot, player, code);
                break;
            case "amount":
                if (mApi.Side == EnumAppSide.Client) return true;
                int amount = parameters["amount"].AsInt(1);
                bool putAmount = parameters["put"].AsBool(false);
                List<ItemStack> amountStacks = TakeAmount(slot, player, amount);
                if (putAmount && mApi.Side != EnumAppSide.Client) Put(amountStacks, player);
                break;
            case "durability":
                if (mApi.Side == EnumAppSide.Client) return true;
                int durability = parameters["durability"].AsInt(1);
                bool destroy = parameters["destroy"].AsBool(true);
                bool overflow = parameters["overflow"].AsBool(false);
                bool put = parameters["put"].AsBool(false);
                List<ItemStack> stacks = TakeDurability(slot, player, durability, destroy, overflow);
                if (put && mApi.Side != EnumAppSide.Client) Put(stacks, player);
                break;
            case "put":
                if (mApi.Side == EnumAppSide.Client) return true;
                Put(slot, player);
                break;
            case "clear":
                Clear(slot);
                break;
            default:
                LogActions(action, "check", "take", "put", "clear", "empty", "spend");
                return false;
        }

        return true;
    }
    public override string[] GetDescription(ItemSlot slot, IWorldAccessor world)
    {
        List<string> output = new();

        foreach ((string code, List<IRequirement>? requirements) in mRequirements)
        {
            string descriptionTemplate = mDescriptions[code];
            if (descriptionTemplate == null) continue;

            List<string> requirementDescriptions = new();
            foreach (IRequirement requirement in requirements)
            {
                requirementDescriptions.Add(requirement?.ToString() ?? "");
            }

            output.Add(Lang.Get(descriptionTemplate, requirementDescriptions.ToArray()));
        }

        return output.ToArray();
    }

    public bool Empty(ItemSlot slot)
    {
        return !ReadStacks(slot).Any();
    }
    public List<ItemStack> Get(ItemSlot slot, IPlayer player) => ReadStacks(slot);
    public List<ItemStack> TakeAll(ItemSlot slot, IPlayer player)
    {
        List<ItemStack> result = ReadStacks(slot);
        ClearStacks(slot);
        return result;
    }
    public List<ItemStack> TakeAmount(ItemSlot slot, IPlayer player, int amount)
    {
        List<ItemStack> stacks = ReadStacks(slot);
        List<ItemSlot> slots = stacks.Select((stack) => new DummySlot(stack) as ItemSlot).ToList();
        List<ItemStack> result = AmountRequirement.CollectFromSlots(slots, amount);
        List<ItemStack> remained = slots.Where((slot) => slot.StackSize > 0).Select((slot) => slot.Itemstack).ToList();
        ClearStacks(slot);
        WriteStacks(slot, remained);
        return result;
    }
    public List<ItemStack> TakeDurability(ItemSlot slot, IPlayer player, int durability, bool destroy = true, bool overflow = false)
    {
        List<ItemStack> stacks = ReadStacks(slot);
        List<ItemSlot> slots = stacks.Select((stack) => new DummySlot(stack) as ItemSlot).ToList();
        List<ItemStack> result = DurabilityRequirement.CollectFromSlots(slots, player, durability, destroy, overflow);
        List<ItemStack> remained = slots.Where((slot) => slot.StackSize > 0).Select((slot) => slot.Itemstack).ToList();
        ClearStacks(slot);
        WriteStacks(slot, remained);
        return result;
    }
    public void Put(ItemSlot slot, IPlayer player, List<ItemStack> items)
    {
        ClearStacks(slot);
        WriteStacks(slot, items);
    }
    public void Clear(ItemSlot slot, IPlayer player)
    {
        ClearStacks(slot);
    }

    private void SendMessage(IPlayer player, IRequirement requirement)
    {
        if (mApi.Side == EnumAppSide.Client) return;
        
        string? requirementName = requirement.ToString();
        if (requirementName == null || requirementName == "") return;

        string message = Lang.GetL((player as IServerPlayer)?.LanguageCode ?? "en", "fsmlib:requirements-missing", requirementName);
        if (message == "") return;

        (player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, message, EnumChatType.Notification);
    }

    private void Take(ItemSlot slot, IPlayer player, string code)
    {
        List<ItemStack> stacks = new();

        foreach (IRequirement requirement in mRequirements[code])
        {
            List<ItemStack> requirementStacks = requirement.Process(player);

            stacks.AddRange(requirementStacks);
        }

        ClearStacks(slot);
        WriteStacks(slot, stacks);
    }
    private void Put(ItemSlot slot, IPlayer player)
    {
        List<ItemStack> stacks = ReadStacks(slot);

        foreach (ItemStack stack in stacks)
        {
            if (!player.Entity.TryGiveItemStack(stack) && mApi.Side == EnumAppSide.Server)
            {
                mApi.World.SpawnItemEntity(stack, player.Entity.SidedPos.XYZ, player.Entity.SidedPos.Motion);
            }
        }
    }
    private void Put(List<ItemStack> stacks, IPlayer player)
    {
        foreach (ItemStack stack in stacks)
        {
            if (!player.Entity.TryGiveItemStack(stack) && mApi.Side == EnumAppSide.Server)
            {
                mApi.World.SpawnItemEntity(stack, player.Entity.SidedPos.XYZ, player.Entity.SidedPos.Motion);
            }
        }
    }
    private void Clear(ItemSlot slot)
    {
        ClearStacks(slot);
    }

    private void WriteStacks(ItemSlot slot, List<ItemStack> stacks)
    {
        slot.Itemstack.Attributes.SetInt($"{mStackAttrName}.count", stacks.Count);
        for (int index = 0; index < stacks.Count; index++)
        {
            slot.Itemstack.Attributes.SetItemstack($"{mStackAttrName}.{index}", stacks[index]);
        }
        slot.MarkDirty();
    }
    private List<ItemStack> ReadStacks(ItemSlot slot)
    {
        List<ItemStack> stacks = new();

        if (!slot.Itemstack.Attributes.HasAttribute($"{mStackAttrName}.count")) return stacks;

        int count = slot.Itemstack.Attributes.GetInt($"{mStackAttrName}.count");
        for (int index = 0; index < count; index++)
        {
            ItemStack? stack = slot.Itemstack.Attributes.GetItemstack($"{mStackAttrName}.{index}", null);
            stack?.ResolveBlockOrItem(mApi.World);
            if (stack != null) stacks.Add(stack);
        }

        return stacks;
    }
    private void ClearStacks(ItemSlot slot)
    {
        if (!slot.Itemstack.Attributes.HasAttribute($"{mStackAttrName}.count")) return;

        int count = slot.Itemstack.Attributes.GetInt($"{mStackAttrName}.count");
        for (int index = 0; index < count; index++)
        {
            slot.Itemstack.Attributes.RemoveAttribute($"{mStackAttrName}.{index}");
        }
        slot.Itemstack.Attributes.RemoveAttribute($"{mStackAttrName}.count");
    }
}
