using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public sealed class ActionInputInvoker : IInputInvoker, IActionInputInvoker
{
    private readonly Dictionary<IInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<EnumEntityAction, List<IActionInput>> mInputs = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private readonly Dictionary<EnumEntityAction, bool> mActionsStatuses = new();
    private readonly ICoreClientAPI mClientApi;

    private readonly Dictionary<EnumEntityAction, EnumEntityAction> mModifiersRemapping = new()
    {
        { EnumEntityAction.ShiftKey, EnumEntityAction.Sneak },
        { EnumEntityAction.CtrlKey, EnumEntityAction.Sprint }
    };
    private readonly HashSet<EnumEntityAction> mModifiers = new()
    {
        EnumEntityAction.ShiftKey,
        EnumEntityAction.CtrlKey,
    };

    public ActionInputInvoker(ICoreClientAPI api)
    {
        mClientApi = api;
        api.Input.InWorldAction += OnEntityAction;

        for (EnumEntityAction action = EnumEntityAction.Forward; action <= EnumEntityAction.InWorldRightMouseDown; action++)
        {
            mActionsStatuses[action] = false;
        }
    }

    public bool IsActive(EnumEntityAction action, bool asModifier = false)
    {
        if (asModifier && mModifiers.Contains(action) && !mClientApi.Settings.Bool.Get("separateCtrlKeyForMouse"))
        {
            return mActionsStatuses[action] || mActionsStatuses[mModifiersRemapping[action]];
        }
        else
        {
            return mActionsStatuses[action];
        }
    }

    private void OnEntityAction(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (!mInputs.ContainsKey(action)) return;

        if (on) mActionsStatuses[action] = true;

        foreach (IActionInput input in mInputs[action].Where(input => TestInput(input, on)))
        {
            if (HandleInput(input))
            {
                handled = EnumHandling.Handled;
                return;
            }
        }

        if (!on) mActionsStatuses[action] = false;
    }

    private bool TestInput(IActionInput input, bool on)
    {
        if (!input.CheckModifiers(mClientApi.World.Player, mClientApi)) return false;
        if (input.OnRelease == on) return false;

        if (input.Modifiers && !mClientApi.Settings.Bool.Get("separateCtrlKeyForMouse"))
        {
            foreach (EnumEntityAction action in input.Actions.Where(action => !mModifiers.Contains(action)))
            {
                if (!mActionsStatuses[action]) return false;
            }

            foreach (EnumEntityAction action in input.Actions.Where(action => mModifiers.Contains(action)))
            {
                if (!mActionsStatuses[action] && !mActionsStatuses[mModifiersRemapping[action]]) return false;
            }
        }

        foreach (EnumEntityAction action in input.Actions)
        {
            if (!mActionsStatuses[action]) return false;
        }

        return true;
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is not IActionInput actionInput) return;

        foreach (EnumEntityAction action in actionInput.Actions)
        {
            mInputs.TryAdd(action, new());
            mInputs[action].Add(actionInput);
        }

        mCallbacks.Add(input, callback);
        mCollectibles.Add(input, collectible);

    }

    public void Dispose()
    {
        mClientApi.Input.InWorldAction -= OnEntityAction;
    }

    private bool HandleInput(IInput input)
    {
        if (mClientApi.World?.Player == null) return false;

        SlotType slotType = input.Slot;

        IEnumerable<SlotData> slots = SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

        bool handled = false;
        foreach (SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input)))
        {
            handled = true;
        }

        return handled;
    }
}
