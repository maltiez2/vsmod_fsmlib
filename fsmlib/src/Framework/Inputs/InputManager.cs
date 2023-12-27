using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using static MaltiezFSM.API.IInputManager;

#nullable enable

namespace MaltiezFSM.Framework;

public sealed class InputManager : IInputManager
{
    private readonly List<IInput> mInputs = new();
    private readonly List<InputCallback> mCallbacks = new();
    private readonly List<CollectibleObject> mCollectibles = new();
    private readonly List<(Type, IInputInvoker)> mInputInvokers = new();
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
        mInputInvokers.Add((inputType, invoker));
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

            mClientPacketSender?.Dispose();
            mServerPacketSender?.Dispose();

            mDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}