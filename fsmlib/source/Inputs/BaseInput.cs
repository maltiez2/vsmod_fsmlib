﻿using MaltiezFSM.API;
using MaltiezFSM.Framework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace MaltiezFSM.Inputs;

public struct BaseInputProperties
{
    public bool Handle { get; set; } = true;
    public SlotType Slot { get; set; } = SlotType.MainHand;
    public EnumModifierKey KeyModifiers { get; set; } = 0;
    public IStatusModifier.StatusType[]? Statuses { get; set; } = null;
    public string[]? Activities { get; set; } = null;

    public IKeyModifier.KeyModifierType KeyModifiersCheckType { get; set; } = IKeyModifier.KeyModifierType.Present;
    public IStandardInput.MultipleCheckType StatusesCheckType { get; set; } = IStandardInput.MultipleCheckType.AtLeastOne;
    public IStandardInput.MultipleCheckType ActivitiesCheckType { get; set; } = IStandardInput.MultipleCheckType.AtLeastOne;

    public BaseInputProperties()
    {

    }

    public static BaseInputProperties Default() => new();
}

public class BaseInput : FactoryProduct, IStandardInput
{
    public int Index { get; set; }
    public bool Handle => mHandled;
    public SlotType Slot => mSlotType;
    public virtual WorldInteraction? GetInteractionInfo(ItemSlot slot) => null;

    public CollectibleObject Collectible { get; }

    public IKeyModifier.KeyModifierType ModifierType { get; }
    public EnumModifierKey ModifierKey { get; }
    public IStandardInput.MultipleCheckType StatusCheckType { get; }
    public IStatusModifier.StatusType[] Statuses { get; }
    public IStandardInput.MultipleCheckType ActivityCheckType { get; }
    public string[] Activities { get; }

    private readonly bool mHandled;
    private readonly SlotType mSlotType;
    private readonly bool mCheckModifiers;
    private readonly bool mCheckStatuses;
    private readonly bool mCheckActivities;
    private readonly IActionInputInvoker? mStatusInvoker;

    public BaseInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        Collectible = collectible;

        mStatusInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>(true)?.GetActionInvoker();

        mHandled = definition["handle"].AsBool(true);
        mSlotType = (SlotType)Enum.Parse(typeof(SlotType), definition["slot"].AsString("MainHand"));

        ModifierType = (IKeyModifier.KeyModifierType)Enum.Parse(typeof(IKeyModifier.KeyModifierType), definition["modifierType"].AsString("Present"));

        List<JsonObject> modifierKeys = ParseField(definition, "modifierKeys");
        if (modifierKeys.Count > 0)
        {
            ModifierKey = modifierKeys.Select(key => (EnumModifierKey)Enum.Parse(typeof(EnumModifierKey), key.AsString())).Aggregate((first, second) => first | second);
        }
        else
        {
            ModifierKey = 0;
        }

        mCheckModifiers = modifierKeys.Any();

        List<JsonObject> statuses = ParseField(definition, "statuses");
        Statuses = statuses.Select(key => (IStatusModifier.StatusType)Enum.Parse(typeof(IStatusModifier.StatusType), key.AsString())).ToArray();
        mCheckStatuses = statuses.Any();

        List<JsonObject> activities = ParseField(definition, "activities");
        Activities = activities.Select(activity => activity.AsString()).ToArray();
        mCheckActivities = activities.Any();

        StatusCheckType = (IStandardInput.MultipleCheckType)Enum.Parse(typeof(IStandardInput.MultipleCheckType), definition["statusType"].AsString("AtLeastOne"));
        ActivityCheckType = (IStandardInput.MultipleCheckType)Enum.Parse(typeof(IStandardInput.MultipleCheckType), definition["activityType"].AsString("AtLeastOne"));
    }
    public BaseInput(
        ICoreAPI api,
        string code,
        CollectibleObject collectible,
        BaseInputProperties? properties = null
        ) : base(GetId(api), code, null, collectible, api)
    {
        BaseInputProperties propertiesOrDefaults = properties ?? BaseInputProperties.Default();

        Collectible = collectible;
        mStatusInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>(true)?.GetActionInvoker();
        mHandled = propertiesOrDefaults.Handle;
        mSlotType = propertiesOrDefaults.Slot;
        ModifierType = propertiesOrDefaults.KeyModifiersCheckType;
        ModifierKey = propertiesOrDefaults.KeyModifiers;
        StatusCheckType = propertiesOrDefaults.StatusesCheckType;
        Statuses = propertiesOrDefaults.Statuses ?? Array.Empty<IStatusModifier.StatusType>();
        mCheckStatuses = Statuses.Any();
        ActivityCheckType = propertiesOrDefaults.ActivitiesCheckType;
        Activities = propertiesOrDefaults.Activities ?? Array.Empty<string>();
        mCheckActivities = Activities.Any();
    }

    public bool CheckModifiers(IPlayer? player, ICoreClientAPI? api)
    {
        if (mCheckModifiers && api != null && mStatusInvoker != null && !InputsUtils.TestModifiers(this, mStatusInvoker, api)) return false;
        if (mCheckStatuses && player != null && !InputsUtils.TestStatus(this, player)) return false;
        if (mCheckActivities && player != null && !InputsUtils.TestActivities(this, player)) return false;
        return true;
    }

    private static int GetId(ICoreAPI api) => api.ModLoader.GetModSystem<FiniteStateMachineSystem>(true).GenerateUniqueInputId();
}
