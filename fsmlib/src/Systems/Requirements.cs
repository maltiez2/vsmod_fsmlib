using MaltiezFSM.Systems.RequirementsApi;
using Newtonsoft.Json.Linq;
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

            mDescriptions.Add(packCode, pack["description"].AsString());
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

        string code = parameters["requirement"].AsString();

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
                (player as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL((player as IServerPlayer)?.LanguageCode ?? "en" , "fsmlib:requirements-missing", requirement.ToString()), EnumChatType.Notification);
            }
        }

        return fulfilled;
    }
    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string? code = parameters["requirement"].AsString();
        string action = parameters["action"].AsString("check");

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

        switch (action)
        {
            case "check":
                break;
            case "take":
                Take(slot, player, code);
                break;
            case "put":
                Put(slot, player);
                break;
            case "clear":
                Clear(slot);
                break;
            default:
                LogActions(action, "check", "take", "put", "clear");
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
            if (!player.Entity.TryGiveItemStack(stack))
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
