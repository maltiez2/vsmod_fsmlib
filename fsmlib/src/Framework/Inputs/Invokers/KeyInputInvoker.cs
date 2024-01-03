using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using static MaltiezFSM.API.IKeyInput;
using static MaltiezFSM.API.IMouseInput;



namespace MaltiezFSM.Framework;

public sealed class KeyInputInvoker : IInputInvoker
{
    private enum InputType
    {
        Unknown,
        KeyDown,
        KeyUp,
        MouseDown,
        MouseUp,
        MouseMove,
        Count
    }

    private readonly KeyToHotkeyMapper mHotkeyMapper;
    private readonly ICoreClientAPI mClientApi;
    private readonly static Type? rHudMouseToolsType = typeof(Vintagestory.Client.NoObf.ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.HudMouseTools");
    private readonly HashSet<string> rBlockingGuiDialogs = new();
    private readonly Dictionary<InputType, List<IInput>> mInputs = new();
    private readonly Dictionary<IInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private bool mDisposed;

    public KeyInputInvoker(ICoreClientAPI api)
    {
        mClientApi = api;
        mClientApi.Event.KeyDown += HandleKeyDown;
        mClientApi.Event.KeyUp += HandleKeyUp;
        mClientApi.Event.MouseDown += HandleMouseDown;
        mClientApi.Event.MouseUp += HandleMouseUp;
        mClientApi.Event.MouseMove += HandleMouseMove;

        mHotkeyMapper = new(api);

        for (InputType input = InputType.Unknown + 1; input < InputType.Count; input++)
        {
            mInputs.Add(input, new());
        }
    }
    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        InputType inputType = InputType.Unknown;

        if (input is IKeyInput keyInput)
        {
            inputType = GetInputType(keyInput.EventType);
            mHotkeyMapper.RegisterKeyInput(keyInput);
        }
        else if (input is IMouseInput mouseInput)
        {
            inputType = GetInputType(mouseInput.EventType);
        }

        if (mInputs.ContainsKey(inputType))
        {
            mInputs[inputType].Add(input);
            mCallbacks.Add(input, callback);
            mCollectibles.Add(input, collectible);
        }
    }

    private static InputType GetInputType(KeyEventType eventType)
    {
        return eventType switch
        {
            KeyEventType.KeyDown => InputType.KeyDown,
            KeyEventType.KeyUp => InputType.KeyUp,
            _ => InputType.Unknown,
        };
    }
    private static InputType GetInputType(MouseEventType eventType)
    {
        return eventType switch
        {
            MouseEventType.MouseDown => InputType.MouseDown,
            MouseEventType.MouseUp => InputType.MouseUp,
            MouseEventType.MouseMove => InputType.MouseMove,
            _ => InputType.Unknown,
        };
    }

    private void HandleKeyDown(KeyEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyDown])
        {
            if (input is not IKeyInput keyInput) continue;

            if (!keyInput.CheckIfShouldBeHandled(eventData, KeyEventType.KeyDown)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleKeyUp(KeyEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyUp])
        {
            if (input is not IKeyInput keyInput) continue;

            if (!keyInput.CheckIfShouldBeHandled(eventData, KeyEventType.KeyUp)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseDown(MouseEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyDown])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseDown)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseUp(MouseEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyDown])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseUp)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseMove(MouseEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyDown])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseMove)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }

    private bool HandleInput(IInput input)
    {
        Utils.SlotType slotType = input.Slot;

        IEnumerable<Utils.SlotData> slots = Utils.SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

        bool handled = false;
        foreach (Utils.SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input))) // Unreadable but now warning... I guess win win?
        {
            handled = true;
        }

        return handled;
    }

    private bool EventShouldBeHandled()
    {
        foreach (GuiDialog item in mClientApi.Gui.OpenedGuis)
        {
            if (item is HudElement) continue;
            if (item.GetType().IsAssignableFrom(rHudMouseToolsType)) continue;
            if (item is Vintagestory.GameContent.GuiDialogWorldMap) continue;

            if (!rBlockingGuiDialogs.Contains(item.DebugName))
            {
                mClientApi.Logger.Debug("[FSMlib] [InputManager] [ClientIfEventShouldBeHandled()] Input was not handled due to opened: " + item.DebugName);
                rBlockingGuiDialogs.Add(item.DebugName);
            }

            return false;
        }

        if (mClientApi.IsGamePaused)
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            mClientApi.Event.KeyDown -= HandleKeyDown;
            mClientApi.Event.KeyUp -= HandleKeyUp;
            mClientApi.Event.MouseDown -= HandleMouseDown;
            mClientApi.Event.MouseUp -= HandleMouseUp;
            mClientApi.Event.MouseMove -= HandleMouseMove;

            mHotkeyMapper.Dispose();

            mDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
