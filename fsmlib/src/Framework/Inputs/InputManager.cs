﻿using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using static MaltiezFSM.API.IInputManager;



namespace MaltiezFSM.Framework;

public sealed class InputManager : IInputManager
{
    private readonly List<IInput> mInputs = new();
    private readonly List<InputCallback> mCallbacks = new();
    private readonly List<CollectibleObject> mCollectibles = new();
    private readonly HashSet<(Type, IInputInvoker)> mInputInvokers = new();
    private readonly InputPacketSenderClient? mClientPacketSender;
    private readonly InputPacketSenderServer? mServerPacketSender;
    private bool mDisposed;
    private const string cNetworkChannelName = "fsmlib.inputManager";

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

    public bool RegisterInvoker(IInputInvoker invoker, Type inputType)
    {
        if (!mInputInvokers.Contains((inputType, invoker)))
        {
            mInputInvokers.Add((inputType, invoker));
            return true;
        }

        return false;
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

    private void PacketHandler(int inputIndex, SlotData slot, IPlayer player)
    {
        if (mInputs.Count > inputIndex)
        {
            _ = InputHandler(slot, player, mInputs[inputIndex], false);
        }
    }
    private bool InputHandler(SlotData slot, IPlayer player, IInput input, bool synchronize = true)
    {
        if (synchronize && mServerPacketSender != null && player is IServerPlayer serverPlayer)
        {
            mServerPacketSender.SendPacket(input.Index, slot, serverPlayer);
        }
        else if (synchronize && mClientPacketSender != null)
        {
            mClientPacketSender.SendPacket(input.Index, slot);
        }

        InputCallback callback = mCallbacks[input.Index];
        ItemSlot? playerSlot = slot.Slot(player);
        if (playerSlot == null) return false;
        bool handled = callback.Invoke(playerSlot, player, input);

#if DEBUG
        InputManagerDebugWindow.Enqueue(slot, player, input, handled, mClientPacketSender != null, !synchronize);
#endif
        return handled;
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            foreach ((_, IInputInvoker invoker) in mInputInvokers)
            {
                invoker.Dispose();
            }

            mClientPacketSender?.Dispose();
            mServerPacketSender?.Dispose();

            mDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}