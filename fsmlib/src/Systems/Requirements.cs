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

public class Requirements : BaseSystem, IItemStackHolder, IRequirementsSystem
{
    protected readonly Dictionary<string, List<IRequirement>> mRequirements = new();
    protected readonly Dictionary<string, string> mDescriptions = new();
    protected readonly string mStackAttrName;
    protected readonly AmmoSelector? mAmmoSelector;

    Dictionary<string, List<IRequirement>> IRequirementsSystem.Requirements => mRequirements;

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

        if (definition.KeyExists("selectorInput") || definition.KeyExists("selectorRequirement"))
        {
            string selectorInput = definition["selectorInput"].AsString("");
            string selectorDescription = definition["selectorDescription"].AsString("");

            mAmmoSelector = new(collectible, api)
            {
                Input = selectorInput,
                Description = selectorDescription,
                Enabled = false
            };

            if (definition.KeyExists("selectorRequirement"))
            {
                string requirement = definition["selectorRequirement"].AsString("");
                mAmmoSelector.Requirements = mRequirements[requirement];
                mAmmoSelector.Enabled = true;
            }
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
        if (mAmmoSelector?.Enabled == true)
        {
            foreach (IRequirement requirement in mAmmoSelector.Requirements)
            {
                if (!requirement.Verify(player))
                {
                    fulfilled = false;
                    SendMessage(player, requirement);
                }
            }

            return fulfilled;
        }

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

                Take(slot, player, mRequirements[code]);
                break;
            case "amount":
                if (mApi.Side == EnumAppSide.Client) return true;
                int amount = parameters["amount"].AsInt(1);
                bool putAmount = parameters["put"].AsBool(false);
                IEnumerable<ItemSlot> amountStacks = TakeAmount(slot, player, amount);
                if (putAmount && mApi.Side != EnumAppSide.Client) Put(amountStacks, player);
                break;
            case "durability":
                if (mApi.Side == EnumAppSide.Client) return true;
                int durability = parameters["durability"].AsInt(1);
                bool destroy = parameters["destroy"].AsBool(true);
                bool overflow = parameters["overflow"].AsBool(false);
                bool put = parameters["put"].AsBool(false);
                IEnumerable<ItemSlot> stacks = TakeDurability(slot, player, durability, destroy, overflow);
                if (put && mApi.Side != EnumAppSide.Client) Put(stacks, player);
                break;
            case "put":
                if (mApi.Side == EnumAppSide.Client) return true;
                Put(slot, player);
                break;
            case "clear":
                Clear(slot);
                break;
            case "setSelector":
                string? codeToSelector = parameters["requirement"].AsString();
                if (codeToSelector == null)
                {
                    LogError($"No 'requirement' in system request");
                    return false;
                }
                if (!mRequirements.ContainsKey(codeToSelector))
                {
                    LogError($"Requirement with code '{codeToSelector}' not found.");
                    return false;
                }
                if (mAmmoSelector == null) return false;

                mAmmoSelector.Requirements = mRequirements[codeToSelector];
                mAmmoSelector.Enabled = true;
                break;
            case "disableSelector":
                if (mAmmoSelector == null) return false;
                mAmmoSelector.Enabled = false;
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
    public IEnumerable<ItemSlot> Get(ItemSlot slot, IPlayer player) => ReadStacks(slot);
    public IEnumerable<ItemSlot> TakeAll(ItemSlot slot, IPlayer player)
    {
        IEnumerable<ItemSlot> result = ReadStacks(slot);
        ClearStacks(slot);
        return result;
    }
    public IEnumerable<ItemSlot> TakeAmount(ItemSlot slot, IPlayer player, int amount)
    {
        IEnumerable<ItemSlot> stacks = ReadStacks(slot);
        IEnumerable<ItemSlot> result = AmountRequirement.CollectFromSlots(stacks, amount, player.Entity.World);
        IEnumerable<ItemSlot> remained = stacks.Where((slot) => slot.StackSize > 0);
        ClearStacks(slot);
        WriteStacks(slot, remained);
        return result;
    }
    public IEnumerable<ItemSlot> TakeDurability(ItemSlot slot, IPlayer player, int durability, bool destroy = true, bool overflow = false)
    {
        IEnumerable<ItemSlot> stacks = ReadStacks(slot);
        IEnumerable<ItemSlot> result = DurabilityRequirement.CollectFromSlots(stacks, player, durability, destroy, overflow);
        IEnumerable<ItemSlot> remained = stacks.Where((slot) => slot.StackSize > 0);
        ClearStacks(slot);
        WriteStacks(slot, remained);
        return result;
    }
    public void Put(ItemSlot slot, IPlayer player, IEnumerable<ItemSlot> items)
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

    protected void Take(ItemSlot slot, IPlayer player, List<IRequirement> requirements)
    {
        List<ItemSlot> stacks = new();

        bool selected = true;
        if (mAmmoSelector?.Enabled == true)
        {
            stacks.Add(mAmmoSelector.Process(player));
            if (stacks[0]?.Itemstack == null || stacks[0].Itemstack.StackSize == 0) selected = false;
        }
        
        if (!selected)
        {
            foreach (IRequirement requirement in requirements)
            {
                IEnumerable<ItemSlot> requirementStacks = requirement.Process(player);

                stacks.AddRange(requirementStacks);
            }
        }

        ClearStacks(slot);
        WriteStacks(slot, stacks);
    }
    protected void Put(ItemSlot slot, IPlayer player)
    {
        IEnumerable<ItemSlot> stacks = ReadStacks(slot);

        Put(stacks, player);
    }
    protected void Put(IEnumerable<ItemSlot> stacks, IPlayer player)
    {
        foreach (ItemStack stack in stacks.Select(slot => slot.Itemstack))
        {
            if (!player.Entity.TryGiveItemStack(stack) && mApi.Side == EnumAppSide.Server)
            {
                mApi.World.SpawnItemEntity(stack, player.Entity.SidedPos.XYZ, player.Entity.SidedPos.Motion);
            }
        }
    }
    protected void Clear(ItemSlot slot)
    {
        ClearStacks(slot);
    }

    private void WriteStacks(ItemSlot slot, IEnumerable<ItemSlot> stacks)
    {
        WriteStacks(slot, stacks.Select(slot => slot.Itemstack).ToList());
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
    private IEnumerable<ItemSlot> ReadStacks(ItemSlot slot)
    {
        List<ItemStack> stacks = new();

        if (!slot.Itemstack.Attributes.HasAttribute($"{mStackAttrName}.count")) return new List<ItemSlot>();

        int count = slot.Itemstack.Attributes.GetInt($"{mStackAttrName}.count");
        for (int index = 0; index < count; index++)
        {
            ItemStack? stack = slot.Itemstack.Attributes.GetItemstack($"{mStackAttrName}.{index}", null);
            stack?.ResolveBlockOrItem(mApi.World);
            if (stack != null) stacks.Add(stack);
        }

        return stacks.Select(stack => new DummySlot(stack));
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
