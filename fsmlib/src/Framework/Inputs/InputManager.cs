using MaltiezFSM.API;
using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using static MaltiezFSM.API.IInputManager;

#nullable enable

namespace MaltiezFSM.Framework
{
    public sealed class InputManager : IInputManager
    {
        private readonly List<IInput> mInputs = new();
        private readonly List<InputCallback> mCallbacks = new();
        private readonly List<CollectibleObject> mCollectibles = new();
        private readonly Dictionary<Type, IInputInvoker> mInputInvokers = new();
        private readonly InputPacketSenderClient? mClientPacketSender;
        private readonly InputPacketSenderServer? mServerPacketSender;
        private bool mDisposed;
        private const string cNetworkChannelName = "maltiezfierarms.inputManager";

        public InputManager(ICoreAPI api)
        {
            if (api is ICoreServerAPI serverAPI)
            {
                mServerPacketSender = new InputPacketSenderServer(serverAPI, PacketHandler, cNetworkChannelName);
            }
            else if (api is ICoreClientAPI clientAPI)
            {
                mClientPacketSender = new InputPacketSenderClient(clientAPI, PacketHandler, cNetworkChannelName);
            }
        }

        public void RegisterInvoker(IInputInvoker invoker, Type inputType)
        {
            mInputInvokers.Add(inputType, invoker);
        }
        public void RegisterInput(IInput input, InputCallback callback, CollectibleObject collectible)
        {
            input.Index = mInputs.Count;

            mInputs.Add(input);
            mCallbacks.Add(callback);
            mCollectibles.Add(collectible);

            foreach ((Type type, IInputInvoker invoker) in mInputInvokers)
            {
                if (type.IsInstanceOfType(input))
                {
                    invoker.RegisterInput(input, InputHandler, collectible);
                }
            }
        }

        private void PacketHandler(int inputIndex, Utils.SlotData slot, IPlayer player)
        {
            if (mInputs.Count > inputIndex)
            {
                _ = InputHandler(slot, player, mInputs[inputIndex]);
            }
        }
        private bool InputHandler(Utils.SlotData slot, IPlayer player, IInput input)
        {
            if (mClientPacketSender != null)
            {
                mClientPacketSender.SendPacket(input.Index, slot);
            }
            else if (mServerPacketSender != null && player is IServerPlayer serverPlayer)
            {
                mServerPacketSender.SendPacket(input.Index, slot, serverPlayer);
            }

            InputCallback callback = mCallbacks[input.Index];
            return callback(slot.Slot(player), player.Entity, input);
        }

        public void Dispose()
        {
            if (!mDisposed)
            {
#pragma warning disable S3966 // Objects should not be disposed more than once
                foreach ((_, IInputInvoker invoker) in mInputInvokers)
                {
                    invoker.Dispose();
                }
#pragma warning restore S3966

                mDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /*private void ClientRegisterEventHandler(IEventInput input, int inputIndex)
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
                if (slotType == IInput.SlotTypes.offHand) return slots;

                ItemSlot hotbarSlot = player?.Entity?.ActiveHandItemSlot?.Inventory[(int)slotId];

                if (hotbarSlot != null)
                {
                    slots.Add(hotbarSlot);
                }
                return slots;
            }

            switch (slotType)
            {
                case IInput.SlotTypes.mainHand:
                    slots.Add(player.Entity.RightHandItemSlot);
                    return slots;
                case IInput.SlotTypes.offHand:
                    slots.Add(player.Entity.LeftHandItemSlot);
                    return slots;
                case IInput.SlotTypes.mouse:
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
            if (mApi is not ICoreClientAPI clientApi) return true;

            foreach (GuiDialog item in clientApi.Gui.OpenedGuis)
            {
                if (item is HudElement) continue;
                if (item.GetType().IsAssignableFrom(rHudMouseToolsType)) continue;
                if (item is Vintagestory.GameContent.GuiDialogWorldMap) continue;

                if (!rBlockingGuiDialogs.Contains(item.DebugName))
                {
                    clientApi.Logger.Debug("[FSMlib] [InputManager] [ClientIfEventShouldBeHandled()] Input was not handled due to opened: " + item.DebugName);
                    rBlockingGuiDialogs.Add(item.DebugName);
                }

                return false;
            }

            if (clientApi.IsGamePaused)
            {
                return false;
            }

            return true;
        }*/
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal struct InputPacket
    {
        public int InputIndex { get; set; }
        public Utils.SlotData Slot { get; set; }
    }

    internal delegate void InputHandler(int inputIndex, Utils.SlotData slot, IPlayer player);

    internal class InputPacketSenderClient
    {
        private readonly InputHandler mHandler;
        private readonly IClientNetworkChannel mClientNetworkChannel;
        private readonly IPlayer mPlayer;

        public InputPacketSenderClient(ICoreClientAPI api, InputHandler handler, string channelName)
        {
            mHandler = handler;
            mPlayer = api.World.Player;

            api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>()
            .SetMessageHandler<InputPacket>(OnClientPacket);

            mClientNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>();
        }
        public void SendPacket(int index, Utils.SlotData slot)
        {
            mClientNetworkChannel.SendPacket(new InputPacket()
            {
                InputIndex = index,
                Slot = slot
            });
        }
        private void OnClientPacket(InputPacket packet)
        {
            mHandler(packet.InputIndex, packet.Slot, mPlayer);
        }
    }
    internal class InputPacketSenderServer
    {
        private readonly InputHandler mHandler;
        private readonly IServerNetworkChannel mServerNetworkChannel;

        public InputPacketSenderServer(ICoreServerAPI api, InputHandler handler, string channelName)
        {
            mHandler = handler;

            api.Network.RegisterChannel(channelName)
                        .RegisterMessageType<InputPacket>()
                        .SetMessageHandler<InputPacket>(OnServerPacket);

            mServerNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>();
        }
        public void SendPacket(int index, Utils.SlotData slot, IServerPlayer player)
        {
            mServerNetworkChannel.SendPacket(new InputPacket()
            {
                InputIndex = index,
                Slot = slot
            }, player);
        }
        private void OnServerPacket(IServerPlayer fromPlayer, InputPacket packet)
        {
            mHandler(packet.InputIndex, packet.Slot, fromPlayer);
        }
    }
}
