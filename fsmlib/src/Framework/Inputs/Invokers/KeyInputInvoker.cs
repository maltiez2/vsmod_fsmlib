using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using VSImGui;
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
        KeyHold,
        MouseDown,
        MouseUp,
        MouseMove,
        MouseHold,
        Count
    }

    private readonly KeyToHotkeyMapper mHotkeyMapper;
    private readonly ICoreClientAPI mClientApi;
    private readonly static Type? rHudMouseToolsType = typeof(Vintagestory.Client.NoObf.ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.HudMouseTools");
    private readonly HashSet<string> rBlockingGuiDialogs = new();
    private readonly Dictionary<InputType, List<IInput>> mInputs = new();
    private readonly Dictionary<IInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private readonly HoldButtonManager mHoldButtonInvoker;
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
        mHoldButtonInvoker = new(api);

        mHoldButtonInvoker.KeyHold += HandleKeyHold;
        mHoldButtonInvoker.MouseHold += HandleMouseHold;

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
            KeyEventType.KeyHold => InputType.KeyHold,
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
            MouseEventType.MouseHold => InputType.MouseHold,
            _ => InputType.Unknown,
        };
    }

    private void HandleKeyDown(KeyEvent eventData)
    {
        mHoldButtonInvoker.HandleKeyDown(eventData);

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
        mHoldButtonInvoker.HandleKeyUp(eventData);

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
        mHoldButtonInvoker.HandleMouseDown(eventData);

        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseDown])
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
        mHoldButtonInvoker.HandleMouseUp(eventData);

        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseUp])
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

        foreach (IInput input in mInputs[InputType.MouseMove])
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
    private void HandleKeyHold(KeyEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.KeyHold])
        {
            if (input is not IKeyInput keyInput) continue;

            if (!keyInput.CheckIfShouldBeHandled(eventData, KeyEventType.KeyHold)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }
    private void HandleMouseHold(MouseEvent eventData)
    {
        if (!EventShouldBeHandled()) return;

        foreach (IInput input in mInputs[InputType.MouseHold])
        {
            if (input is not IMouseInput mouseInput) continue;

            if (!mouseInput.CheckIfShouldBeHandled(eventData, MouseEventType.MouseHold)) continue;

            if (HandleInput(input))
            {
                eventData.Handled = true;
                return;
            }
        }
    }

    private bool HandleInput(IInput input)
    {
        if (mClientApi.World?.Player == null) return false;

        if ((input as IStandardInput)?.CheckModifiers(mClientApi.World.Player, mClientApi) == false) return false;

        SlotType slotType = input.Slot;

        IEnumerable<SlotData> slots = SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

        bool handled = false;
        foreach (SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input)))
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

internal sealed class HoldButtonManager : IDisposable
{
    public event Action<MouseEvent>? MouseHold;
    public event Action<KeyEvent>? KeyHold;

    private const int cTimerDelay = 0; // 2+ game ticks
    private readonly long mTimer;

    private readonly Dictionary<EnumMouseButton, MouseEvent?> mMouseButtons = new();
    private readonly Dictionary<GlKeys, KeyEvent?> mKeyboardButtons = new();
    private readonly Dictionary<EnumMouseButton, bool> mMouseButtonsDelay = new();
    private readonly Dictionary<GlKeys, bool> mKeyboardButtonsDelay = new();
    private bool mDisposed = false;
    private readonly ICoreClientAPI mApi;

    public HoldButtonManager(ICoreClientAPI api)
    {
        mApi = api;
        mTimer = api.World.RegisterGameTickListener(InvokeEvents, cTimerDelay);
    }

    public void HandleKeyDown(KeyEvent eventData)
    {
        GlKeys key = (GlKeys)eventData.KeyCode;
        if (!mKeyboardButtons.ContainsKey(key) || mKeyboardButtons[key] == null) mKeyboardButtonsDelay[key] = false;
        mKeyboardButtons[key] = eventData;
    }
    public void HandleKeyUp(KeyEvent eventData)
    {
        GlKeys key = (GlKeys)eventData.KeyCode;
        mKeyboardButtons[key] = null;
    }
    public void HandleMouseDown(MouseEvent eventData)
    {
        EnumMouseButton key = eventData.Button;
        if (!mMouseButtons.ContainsKey(key) || mMouseButtons[key] == null) mMouseButtonsDelay[key] = false;
        mMouseButtons[key] = eventData;
    }
    public void HandleMouseUp(MouseEvent eventData)
    {
        EnumMouseButton key = eventData.Button;
        mMouseButtons[key] = null;
    }

    private void InvokeEvents(float dt)
    {
        foreach ((EnumMouseButton key, MouseEvent? mouseEvent) in mMouseButtons)
        {
            if (mouseEvent != null)
            {
                if (mMouseButtonsDelay[key]) MouseHold?.Invoke(mouseEvent);
                mMouseButtonsDelay[key] = true;
            }
        }

        foreach ((GlKeys key, KeyEvent? keyEvent) in mKeyboardButtons)
        {
            if (keyEvent != null)
            {
                if (mKeyboardButtonsDelay[key]) KeyHold?.Invoke(keyEvent);
                mKeyboardButtonsDelay[key] = true;
            }
        }
    }

    public void Dispose()
    {
        if (mDisposed) return;
        GC.SuppressFinalize(this);
        mDisposed = true;

        mApi.World.UnregisterGameTickListener(mTimer);
    }
}
