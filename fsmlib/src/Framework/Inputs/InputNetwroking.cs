using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

#nullable enable

namespace MaltiezFSM.Framework;

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