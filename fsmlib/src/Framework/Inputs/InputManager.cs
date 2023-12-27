using MaltiezFSM.API;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal class AggregatedInputPacket
{
    public InputPacket[] Packets { get; set; } = Array.Empty<InputPacket>();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct InputPacket
{
    public int InputIndex { get; set; }
    public Utils.SlotData Slot { get; set; }
}

internal delegate void InputHandler(int inputIndex, Utils.SlotData slot, IPlayer player);

internal sealed class InputPacketSenderClient : IDisposable
{
    private readonly InputHandler mHandler;
    private readonly IClientNetworkChannel mClientNetworkChannel;
    private readonly IPlayer mPlayer;
    private readonly List<InputPacket> mAggregationQueue = new();
    private readonly long mListener;
    private readonly ICoreClientAPI mClientApi;
    private bool mDisposed = false;

    public InputPacketSenderClient(ICoreClientAPI api, InputHandler handler, string channelName)
    {
        mHandler = handler;
        mPlayer = api.World.Player;
        mClientApi = api;

        api.Network.RegisterChannel(channelName)
            .RegisterMessageType<AggregatedInputPacket>()
            .SetMessageHandler<AggregatedInputPacket>(OnClientPacket);

        mClientNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<AggregatedInputPacket>();

        mListener = api.World.RegisterGameTickListener(SendAggregatedPacket, 0);
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mClientApi.World.UnregisterCallback(mListener);
    }

    public void SendPacket(int index, Utils.SlotData slot)
    {
        mAggregationQueue.Add(new InputPacket()
        {
            InputIndex = index,
            Slot = slot
        });
    }
    private void OnClientPacket(AggregatedInputPacket aggregatedPacket)
    {
        foreach (InputPacket packet in aggregatedPacket.Packets)
        {
            mHandler(packet.InputIndex, packet.Slot, mPlayer);
        }
    }
    private void SendAggregatedPacket(float dt)
    {
        mClientNetworkChannel.SendPacket(new AggregatedInputPacket()
        {
            Packets = mAggregationQueue.ToArray()
        });
        mAggregationQueue.Clear();
    }
}

internal sealed class InputPacketSenderServer : IDisposable
{
    private readonly InputHandler mHandler;
    private readonly IServerNetworkChannel mServerNetworkChannel;
    private readonly Dictionary<IServerPlayer, List<InputPacket>> mAggregationQueue = new();
    private readonly long mListener;
    private readonly ICoreServerAPI mServerApi;
    private bool mDisposed = false;

    public InputPacketSenderServer(ICoreServerAPI api, InputHandler handler, string channelName)
    {
        mHandler = handler;
        mServerApi = api;

        api.Network.RegisterChannel(channelName)
            .RegisterMessageType<AggregatedInputPacket>()
            .SetMessageHandler<AggregatedInputPacket>(OnServerPacket);

        mServerNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<AggregatedInputPacket>();

        mListener = api.World.RegisterGameTickListener(SendAggregatedPacket, 0);
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mServerApi.World.UnregisterCallback(mListener);
    }

    public void SendPacket(int index, Utils.SlotData slot, IServerPlayer player)
    {
        if (!mAggregationQueue.ContainsKey(player)) mAggregationQueue.Add(player, new());

        mAggregationQueue[player].Add(new InputPacket()
        {
            InputIndex = index,
            Slot = slot
        });
    }
    private void OnServerPacket(IServerPlayer fromPlayer, AggregatedInputPacket aggregatedPacket)
    {
        foreach (InputPacket packet in aggregatedPacket.Packets)
        {
            mHandler(packet.InputIndex, packet.Slot, fromPlayer);
        }
    }
    private void SendAggregatedPacket(float dt)
    {
        foreach ((var player, var packet) in mAggregationQueue)
        {
            mServerNetworkChannel.SendPacket(new AggregatedInputPacket()
            {
                Packets = packet.ToArray()
            }, player);
            mAggregationQueue[player].Clear();
        }
    }
}