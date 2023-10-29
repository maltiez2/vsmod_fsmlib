using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using static MaltiezFSM.API.IInputManager;
using static MaltiezFSM.API.IKeyInput;
using static MaltiezFSM.API.IMouseInput;
using static MaltiezFSM.API.ISlotChangedAfter;
using MaltiezFSM.API;
using Vintagestory.GameContent;
using System.Numerics;

namespace MaltiezFSM.Framework
{
    public class InputManager : IInputManager
    {
        private readonly ICoreClientAPI mClientApi;
        private readonly IActiveSlotListener mSlotListener;
        private readonly IHotkeyInputManager mHotkeyInputManager;
        private readonly IStatusInputManager mStatusInputManager;
        private readonly IKeyInputManager mKeyManager;
        private readonly List<IInput> mInputs = new();
        private readonly List<InputCallback> mCallbacks = new();
        private readonly List<CollectibleObject> mCollectibles = new();
        private readonly InputPacketSender mPacketSender;

        private const string cNetworkChannelName = "maltiezfierarms.inputManager";

        // Whitelisted GuiDialogs
        private readonly static Type rHudMouseToolsType = typeof(Vintagestory.Client.NoObf.ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.HudMouseTools");
        private readonly HashSet<string> rBlockingGuiDialogs = new HashSet<string>();

        public InputManager(ICoreAPI api, IActiveSlotListener slotListener, IHotkeyInputManager hotkeyManager, IStatusInputManager statusManager, IKeyInputManager keyManager)
        {
            mPacketSender = new InputPacketSender(api, ServerInputProxyHandler, cNetworkChannelName);
            mHotkeyInputManager = hotkeyManager;
            mStatusInputManager = statusManager;
            mKeyManager = keyManager;

            if (api.Side == EnumAppSide.Client)
            {
                mClientApi = api as ICoreClientAPI;
                mSlotListener = slotListener;
            }
        }

        public void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible)
        {
            int inputIndex = mInputs.Count;

            mInputs.Add(input);
            mCallbacks.Add(callback);
            mCollectibles.Add(collectible);

            if (input is IKeyInput && mClientApi != null)
            {
                mKeyManager.RegisterKeyInput(input as IKeyInput);
            }

            if (input is IHotkeyInput && mClientApi != null)
            {
                mHotkeyInputManager?.RegisterHotkeyInput(input as IHotkeyInput, _ => ClientInputProxyHandler(inputIndex, null));
            }

            if (input is IEventInput && mClientApi != null)
            {
                ClientRegisterEventHandler(input as IEventInput, inputIndex);
            }

            if (input is IStatusInput && mClientApi != null)
            {
                mStatusInputManager.RegisterStatusInput(input as IStatusInput, _ => ClientInputProxyHandler(inputIndex, null));
            }
        }
        private void ClientRegisterEventHandler(IEventInput input, int inputIndex)
        {
            switch ((input as IKeyInput)?.GetEventType())
            {
                case KeyEventType.KEY_DOWN:
                    mClientApi.Event.KeyDown += (KeyEvent ev) => ClientKeyInputProxyHandler(ev, inputIndex, KeyEventType.KEY_DOWN);
                    break;
                case KeyEventType.KEY_UP:
                    mClientApi.Event.KeyUp += (KeyEvent ev) => ClientKeyInputProxyHandler(ev, inputIndex, KeyEventType.KEY_UP);
                    break;
                case null:
                    break;
            }

            switch ((input as IMouseInput)?.GetEventType())
            {
                case MouseEventType.MOUSE_DOWN:
                    mClientApi.Event.MouseDown += (MouseEvent ev) => ClientMouseInputProxyHandler(ev, inputIndex, MouseEventType.MOUSE_DOWN);
                    break;
                case MouseEventType.MOUSE_UP:
                    mClientApi.Event.MouseUp += (MouseEvent ev) => ClientMouseInputProxyHandler(ev, inputIndex, MouseEventType.MOUSE_UP);
                    break;
                case MouseEventType.MOUSE_MOVE:
                    mClientApi.Event.MouseMove += (MouseEvent ev) => ClientMouseInputProxyHandler(ev, inputIndex, MouseEventType.MOUSE_MOVE);
                    break;
                case null:
                    break;
            }

            if (input is ISlotModified)
            {
                throw new NotImplementedException();
            }
            if (input is ISlotChangedAfter)
            {
                 mClientApi.Event.AfterActiveSlotChanged += (ActiveSlotChangeEventArgs ev) => ClientSlotInputProxyHandler(inputIndex, ev.FromSlot, ev.ToSlot);
            }
            if (input is ISlotChangedBefore)
            {
                mClientApi.Event.BeforeActiveSlotChanged += (ActiveSlotChangeEventArgs ev) => ClientBeforeSlotInputProxyHandler(inputIndex, ev.FromSlot);
            }
            if (input is ISlotEvent)
            {
                mSlotListener.RegisterListener((input as ISlotEvent).GetEventType(), (int slotId) => ClientInputProxyHandler(inputIndex, slotId));
            }
        }

        private List<ItemSlot> GetSlots(int? slotId, IPlayer player, IInput input, CollectibleObject collectible)
        {
            List<ItemSlot> slots = new();
            IInput.SlotTypes slotType = input.SlotType();

            if (slotId != null)
            {
                if (slotType == IInput.SlotTypes.OFF_HAND) return slots;

                ItemSlot hotbarSlot = player?.Entity?.ActiveHandItemSlot?.Inventory[(int)slotId];

                if (hotbarSlot != null)
                {
                    slots.Add(hotbarSlot);
                }
                return slots;
            }

            switch (slotType)
            {
                case IInput.SlotTypes.MAIN_HAND:
                    slots.Add(player.Entity.RightHandItemSlot);
                    return slots;
                case IInput.SlotTypes.OFF_HAND:
                    slots.Add(player.Entity.LeftHandItemSlot);
                    return slots;
                case IInput.SlotTypes.MOUSE:
                    slots.Add(player.InventoryManager.MouseItemSlot);
                    return slots;
                default:
                    player?.Entity?.WalkInventory((inventorySlot) =>
                    {
                        if (inventorySlot is ItemSlotCreative) return true;
                        if (inventorySlot?.Itemstack?.Collectible == collectible) slots.Add(inventorySlot);
                        return true;
                    });
                    return slots;
            }
        }
        public bool ClientKeyInputProxyHandler(KeyEvent ev, int inputIndex, KeyEventType keyEventType)
        {
            if (!(mInputs[inputIndex] as IKeyInput).CheckIfShouldBeHandled(ev, keyEventType)) return false;

            if (!ClientIfEventShouldBeHandled()) return false;

            bool handled = ClientInputProxyHandler(inputIndex, null);
            if (handled) ev.Handled = true;
            
            return handled;
        }
        public bool ClientMouseInputProxyHandler(MouseEvent ev, int inputIndex, MouseEventType keyEventType)
        {
            if (!(mInputs[inputIndex] as IMouseInput).CheckIfShouldBeHandled(ev, keyEventType)) return false;

            if (!ClientIfEventShouldBeHandled()) return false;

            bool handled = ClientInputProxyHandler(inputIndex, null);
            if (handled) ev.Handled = true;
            
            return handled;
        }
        public bool ClientSlotInputProxyHandler(int inputIndex, int fromSlotId, int toSlotId)
        {
            switch ((mInputs[inputIndex] as ISlotChangedAfter).GetEventType())
            {
                case SlotEventType.FROM_WEAPON:
                    return ClientInputProxyHandler(inputIndex, fromSlotId);
                case SlotEventType.TO_WEAPON:
                    return ClientInputProxyHandler(inputIndex, toSlotId);
                default:
                    throw new NotImplementedException();
            }
        }
        public EnumHandling ClientBeforeSlotInputProxyHandler(int inputIndex, int fromSlotId)
        {
            if (ClientInputProxyHandler(inputIndex, fromSlotId))
            {
                return (mInputs[inputIndex] as ISlotChangedBefore).GetHandlingType();
            }

            return EnumHandling.PassThrough;
        }
        public bool ClientInputProxyHandler(int inputIndex, int? slotId)
        {
            mPacketSender.SendPacket(inputIndex, slotId);

            return InputHandler(inputIndex, mClientApi.World.Player, slotId);
        }
        public void ServerInputProxyHandler(int inputIndex, int? slotId, IServerPlayer serverPlayer)
        {
            InputHandler(inputIndex, serverPlayer, slotId);
        }

        private bool InputHandler(int inputIndex, IPlayer player, int? slotId)
        {
            IInput input = mInputs[inputIndex];
            InputCallback callback = mCallbacks[inputIndex];
            List<ItemSlot> slots = GetSlots(slotId, player, input, mCollectibles[inputIndex]);

            foreach (ItemSlot slot in slots)
            {
                if (callback(slot, player.Entity, input))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ClientIfEventShouldBeHandled()
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
    }

    public class InputPacketSender
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class InputPacket
        {
            public int InputIndex;
            public int? SlotId;
        }

        public delegate void InputHandler(int inputIndex, int? slotId, IServerPlayer player);
        private InputHandler mHandler;
        public InputPacketSender(ICoreAPI api, InputHandler handler, string channelName)
        {
            if (api.Side == EnumAppSide.Server)
            {
                StartServerSide(api as ICoreServerAPI, handler, channelName);
            }
            else if (api.Side == EnumAppSide.Client)
            {
                StartClientSide(api as ICoreClientAPI, channelName);
            }
        }

        // SERVER SIDE

        private void StartServerSide(ICoreServerAPI api, InputHandler handler, string channelName)
        {
            mHandler = handler;
            api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>()
            .SetMessageHandler<InputPacket>(OnServerPacket);
        }
        private void OnServerPacket(IServerPlayer fromPlayer, InputPacket packet)
        {
            mHandler(packet.InputIndex, packet.SlotId, fromPlayer);
        }

        // CLIENT SIDE

        IClientNetworkChannel mClientNetworkChannel;

        private void StartClientSide(ICoreClientAPI api, string channelName)
        {
            mClientNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>();
        }
        public void SendPacket(int index, int? slot)
        {
            mClientNetworkChannel.SendPacket(new InputPacket()
            {
                InputIndex = index,
                SlotId = slot
            });
        }
    }
}
