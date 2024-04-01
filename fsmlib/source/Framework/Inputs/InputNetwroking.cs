using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;



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
    public SlotData Slot { get; set; }
}

internal delegate void InputHandler(int inputIndex, SlotData slot, IPlayer player);

internal sealed class InputPacketSenderClient : IDisposable
{
    private readonly InputHandler mHandler;
    private readonly IClientNetworkChannel mClientNetworkChannel;
    private readonly List<InputPacket> mAggregationQueue = new();
    private readonly long mListener;
    private readonly ICoreClientAPI mClientApi;
    private bool mDisposed = false;

    public InputPacketSenderClient(ICoreClientAPI api, InputHandler handler, string channelName)
    {
        mHandler = handler;
        mClientApi = api;

        mClientNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<AggregatedInputPacket>()
            .SetMessageHandler<AggregatedInputPacket>(OnClientPacket);

        mListener = api.World.RegisterGameTickListener(SendAggregatedPacket, 0);
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mClientApi.World.UnregisterCallback(mListener);
    }

    public void SendPacket(int index, SlotData slot)
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
            mHandler(packet.InputIndex, packet.Slot, mClientApi.World.Player);
        }
    }
    private void SendAggregatedPacket(float dt)
    {
#if DEBUG
        InputManagerDebugWindow.EnqueuePacket(mAggregationQueue.Count, true);
#endif
        if (mAggregationQueue.Count == 0) return;
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

        mServerNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<AggregatedInputPacket>()
            .SetMessageHandler<AggregatedInputPacket>(OnServerPacket);

        mListener = api.World.RegisterGameTickListener(SendAggregatedPacket, 0);
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mServerApi.World.UnregisterCallback(mListener);
    }

    public void SendPacket(int index, SlotData slot, IServerPlayer player)
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
        int packets = 0;
        foreach ((IServerPlayer? player, List<InputPacket>? packet) in mAggregationQueue)
        {
            if (packet == null || packet.Count == 0) continue;
            mServerNetworkChannel.SendPacket(new AggregatedInputPacket()
            {
                Packets = packet.ToArray()
            }, player);
            packets += mAggregationQueue[player].Count;
            mAggregationQueue[player].Clear();
        }
#if DEBUG
        InputManagerDebugWindow.EnqueuePacket(packets, false);
#endif
    }
}