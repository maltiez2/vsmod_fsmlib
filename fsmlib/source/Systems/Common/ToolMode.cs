using MaltiezFSM.API;
using MaltiezFSM.Framework;
using MaltiezFSM.Systems.RequirementsApi;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Systems;

public abstract class ToolModes
{
    public SkillItem? GetMode(IPlayer player)
    {
        long entityId = player.Entity?.EntityId ?? -1;
        if (mCurrentModes.ContainsKey(entityId) && mSelectedMode.ContainsKey(entityId)) return mCurrentModes[entityId][mSelectedMode[entityId]];
        return null;
    }
    public bool Enabled { get; set; }

    protected readonly Dictionary<long, SkillItem[]> mCurrentModes = new();
    protected readonly Dictionary<long, int> mSelectedMode = new();
    protected ICoreAPI mApi;

    protected ToolModes(CollectibleObject collectible, ICoreAPI api)
    {
        mApi = api;
        if (collectible.GetCollectibleBehavior(typeof(IToolModeEventProvider), true) is not IToolModeEventProvider behavior) return;
        behavior.OnGetToolModes += ToolModesGetter;
    }

    protected abstract SkillItem[] OnGetToolModes(ItemSlot slot, IPlayer forPlayer, BlockSelection blockSel);
    protected abstract void OnSetToolModes(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, SkillItem toolMode);

    private (ToolModeSetter, SkillItem[]) ToolModesGetter(ItemSlot slot, IPlayer forPlayer, BlockSelection blockSel)
    {
        if (!Enabled) return (ToolModeSetter, Array.Empty<SkillItem>());
        long entityId = forPlayer.Entity.EntityId;
        mCurrentModes[entityId] = OnGetToolModes(slot, forPlayer, blockSel);
        return (ToolModeSetter, mCurrentModes[entityId]);
    }
    private string? ToolModeSetter(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
    {
        if (!Enabled) return null;
        long entityId = byPlayer.Entity.EntityId;
        mSelectedMode[entityId] = toolMode;
        SkillItem mode = mCurrentModes[entityId][toolMode];
        OnSetToolModes(slot, byPlayer, blockSelection, mode);
        return mode.Code.Path;
    }

    protected SkillItem[] GetModesFromSlots(IEnumerable<ItemSlot> slots, System.Func<ItemSlot, string> descriptionGetter, string inputCode = "")
    {
        List<SkillItem> modes = new();
        foreach (ItemSlot slot in slots)
        {
            if (slot.Itemstack == null) continue;
            modes.Add(new SkillItem
            {
                Code = new AssetLocation(inputCode),
                Name = descriptionGetter(slot),
                Data = slot
            });

            if (mApi is ICoreClientAPI clientApi)
            {
                modes[^1].RenderHandler = GetItemStackRenderCallback(slot, clientApi, ColorUtil.WhiteArgb);
            }
        }

        return modes.ToArray();
    }
    protected static RenderSkillItemDelegate GetItemStackRenderCallback(ItemSlot slot, ICoreClientAPI clientApi, int color)
    {
        return (AssetLocation code, float dt, double posX, double posY) =>
        {
            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
            double scaledSize = GuiElement.scaled(size - 5);

            clientApi?.Render.RenderItemstackToGui(
                slot,
                posX + (scaledSize / 2),
                posY + (scaledSize / 2),
                100,
                (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledItemSize),
                color,
                showStackSize: true);
        };
    }
}

public class AmmoSelector : ToolModes
{
    public List<IRequirement> Requirements { get; set; } = new();
    public string Description { get; set; } = "";
    public string Input { get; set; } = "";
    public ItemSlot CurrentSlot { get; set; } = new DummySlot();

    public AmmoSelector(CollectibleObject collectible, ICoreAPI api) : base(collectible, api)
    {

    }

    public ItemSlot Process(IPlayer player)
    {
        if (Requirements.Count == 0) return CurrentSlot;
        
        IRequirement requirement = Requirements[0];

        if (requirement is AmountRequirement amountRequirement)
        {
            ItemSlot result = new DummySlot();
            CurrentSlot.TryPutInto(player.Entity.World, result, amountRequirement.Amount);
            return result;
        }

        if (requirement is DurabilityRequirement durabilityRequirement)
        {
            ItemSlot result = new DummySlot(CurrentSlot.Itemstack.Clone());
            int durability = durabilityRequirement.Durability;
            result.Itemstack.Item.DamageItem(player.Entity.World, player.Entity, result, CurrentSlot.Itemstack.Item.GetRemainingDurability(CurrentSlot.Itemstack) - durability);
            CurrentSlot.Itemstack.Item.DamageItem(player.Entity.World, player.Entity, result, durability);
            return result;
        }

        return new DummySlot(CurrentSlot.Itemstack.Clone());
    }

    protected override SkillItem[] OnGetToolModes(ItemSlot slot, IPlayer forPlayer, BlockSelection blockSel)
    {
        HashSet<ItemSlot> slots = new();

        foreach (IRequirement requirement in Requirements)
        {
            foreach (var processedSlot in requirement.Search(forPlayer, findAll: true))
            {
                if (!slots.Contains(processedSlot)) slots.Add(processedSlot);
            }
        }

        return GetModesFromSlots(slots, slot => Description, Input);
    }
    protected override void OnSetToolModes(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, SkillItem toolMode) => CurrentSlot = (ItemSlot)toolMode.Data;
}