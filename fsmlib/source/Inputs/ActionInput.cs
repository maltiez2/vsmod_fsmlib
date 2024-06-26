﻿using MaltiezFSM.API;
using MaltiezFSM.Framework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Inputs;


public struct ActionInputProperties
{
    public EnumEntityAction[] Actions { get; set; }
    public bool OnRelease { get; set; } = false;
    public bool AsModifiers { get; set; } = false;
    public string Name { get; set; } = "";

    public ActionInputProperties(params EnumEntityAction[] actions)
    {
        Actions = actions;
    }
}

public sealed class ActionInput : BaseInput, IActionInput
{
    public ActionInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        List<JsonObject> actions = ParseField(definition["actions"]);
        Actions = actions.Select(action => (EnumEntityAction)Enum.Parse(typeof(EnumEntityAction), action.AsString())).ToArray();
        OnRelease = definition["onRelease"].AsBool(false);
        Modifiers = definition["asModifiers"].AsBool(false);
        Name = definition["name"].AsString("");

        mInteractionInfo = new()
        {
            ActionLangCode = Name,
            MouseButton = GetMouseButton(),
            HotKeyCodes = GetModifiers()
        };
    }
    public ActionInput(ICoreAPI api, string code, CollectibleObject collectible, ActionInputProperties actionProperties, BaseInputProperties? baseProperties = null) : base(api, code, collectible, baseProperties)
    {
        Actions = actionProperties.Actions;
        OnRelease = actionProperties.OnRelease;
        Modifiers = actionProperties.AsModifiers;
        Name = actionProperties.Name;

        mInteractionInfo = new()
        {
            ActionLangCode = Name,
            MouseButton = GetMouseButton(),
            HotKeyCodes = GetModifiers()
        };
    }

    public EnumEntityAction[] Actions { get; }
    public bool OnRelease { get; }
    public string Name { get; }
    public bool Modifiers { get; }

    public override WorldInteraction GetInteractionInfo(ItemSlot slot)
    {
        return mInteractionInfo;
    }

    private readonly WorldInteraction mInteractionInfo;

    private EnumMouseButton GetMouseButton()
    {
        if (Actions.Contains(EnumEntityAction.LeftMouseDown)) return EnumMouseButton.Left;
        if (Actions.Contains(EnumEntityAction.RightMouseDown)) return EnumMouseButton.Right;
        return EnumMouseButton.None;
    }
    private string[] GetModifiers()
    {
        List<string> modifiers = new();
        if (Actions.Contains(EnumEntityAction.ShiftKey)) modifiers.Add("shift");
        if (Actions.Contains(EnumEntityAction.CtrlKey)) modifiers.Add("ctrl");
        return modifiers.ToArray();
    }

    public override string ToString() => $"Action input: {Utils.PrintList(Actions.Select(action => action.ToString()))}";
}
