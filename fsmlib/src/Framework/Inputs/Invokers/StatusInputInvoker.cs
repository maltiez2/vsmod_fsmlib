﻿using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework;

public sealed class StatusInputInvokerClient : IInputInvoker
{
    private const int cCheckInterval_ms = 33;
    private readonly ICoreClientAPI mClientApi;
    private readonly Dictionary<IStatusInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private readonly long mListener;
    private bool mDisposed = false;

    public StatusInputInvokerClient(ICoreClientAPI api)
    {
        mClientApi = api;
        mListener = mClientApi.World.RegisterGameTickListener(_ => CheckStatuses(), cCheckInterval_ms);
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is not IStatusInput statusInput) return;

        mCallbacks.Add(statusInput, callback);
        mCollectibles.Add(statusInput, collectible);
    }

    private void CheckStatuses()
    {
        if (mClientApi?.World?.Player?.Entity == null) return;

        foreach ((IStatusInput input, _) in mCallbacks)
        {
            try
            {
                ProcessInput(input);
            }
            catch (Exception exception)
            {
                Logger.Error(mClientApi, this, $"Exception while processing input '{input}'");
                Logger.Verbose(mClientApi, this, $"Exception while processing input '{input}'.\n\nException:\n{exception}\n");
            }
        }
    }

    private void ProcessInput(IStatusInput input)
    {
        if (CheckStatus(input) ^ input.Invert)
        {
            _ = HandleInput(input);
        }
    }

    private bool CheckStatus(IStatusInput input)
    {
        if (mClientApi.World.Player.Entity == null || mClientApi.World.Player.Entity.World == null) return false;

        return input.Status switch
        {
            IStatusInput.StatusType.Activity => mClientApi.World.Player.Entity.IsActivityRunning(input.Activity),
            IStatusInput.StatusType.Swimming => mClientApi.World.Player.Entity.Swimming,
            IStatusInput.StatusType.OnFire => mClientApi.World.Player.Entity.IsOnFire,
            IStatusInput.StatusType.Collided => mClientApi.World.Player.Entity.Collided,
            IStatusInput.StatusType.CollidedHorizontally => mClientApi.World.Player.Entity.CollidedHorizontally,
            IStatusInput.StatusType.CollidedVertically => mClientApi.World.Player.Entity.CollidedVertically,
            IStatusInput.StatusType.EyesSubmerged => mClientApi.World.Player.Entity.IsEyesSubmerged(),
            IStatusInput.StatusType.FeetInLiquid => mClientApi.World.Player.Entity.FeetInLiquid,
            IStatusInput.StatusType.InLava => mClientApi.World.Player.Entity.InLava,
            IStatusInput.StatusType.OnGround => mClientApi.World.Player.Entity.OnGround,
            _ => false,
        };
    }

    private bool HandleInput(IStatusInput input)
    {
        SlotType slotType = input.Slot;

        IEnumerable<SlotData> slots = SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

        bool handled = false;
        foreach (SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input, false)))
        {
            handled = true;
        }

        return handled;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mClientApi.World.UnregisterGameTickListener(mListener);
    }
}

public sealed class StatusInputInvokerServer : IInputInvoker
{
    private const int cCheckInterval_ms = 33;
    private readonly ICoreServerAPI mServerApi;
    private readonly Dictionary<IStatusInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private readonly long mListener;
    private bool mDisposed = false;

    public StatusInputInvokerServer(ICoreServerAPI api)
    {
        mServerApi = api;
        mListener = mServerApi.World.RegisterGameTickListener(_ => CheckStatuses(), cCheckInterval_ms);
    }

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is IStatusInput statusInput)
        {
            mCallbacks.Add(statusInput, callback);
            mCollectibles.Add(statusInput, collectible);
        }
    }

    private void CheckStatuses()
    {
        try
        {
            foreach (IPlayer player in mServerApi.World.AllPlayers.Where(player => player?.Entity != null))
            {
                CheckPlayerStatuses(player);
            }
        }
        catch (Exception exception)
        {
            Logger.Debug(mServerApi, this, $"Exception on checking player status:\n{exception}");
        }
    }

    private void CheckPlayerStatuses(IPlayer player)
    {
        foreach ((IStatusInput input, _) in mCallbacks)
        {
            if (CheckPlayerStatus(input, player) ^ input.Invert)
            {
                _ = HandleInput(input, player);
            }
        }
    }

    private bool CheckPlayerStatus(IStatusInput input, IPlayer player)
    {
        if (player.Entity == null || player.Entity.World == null) return false;

        try
        {
            return input.Status switch
            {
                IStatusInput.StatusType.Activity => player.Entity.IsActivityRunning(input.Activity),
                IStatusInput.StatusType.Swimming => player.Entity.Swimming,
                IStatusInput.StatusType.OnFire => player.Entity.IsOnFire,
                IStatusInput.StatusType.Collided => player.Entity.Collided,
                IStatusInput.StatusType.CollidedHorizontally => player.Entity.CollidedHorizontally,
                IStatusInput.StatusType.CollidedVertically => player.Entity.CollidedVertically,
                IStatusInput.StatusType.EyesSubmerged => player.Entity.IsEyesSubmerged(),
                IStatusInput.StatusType.FeetInLiquid => player.Entity.FeetInLiquid,
                IStatusInput.StatusType.InLava => player.Entity.InLava,
                IStatusInput.StatusType.OnGround => player.Entity.OnGround,
                _ => false,
            };
        }
        catch (Exception exception)
        {
#if DEBUG
            Logger.Debug(mServerApi, this, $"Exception on checking player status for input '{input}':\n{exception}");
#endif
        }

        return false;
    }

    private bool HandleInput(IStatusInput input, IPlayer player)
    {
        SlotType slotType = input.Slot;

        IEnumerable<SlotData> slots = SlotData.GetForAllSlots(slotType, mCollectibles[input], player);

        bool handled = false;
        foreach (SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, player, input, false)))
        {
            handled = true;
        }

        return handled;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mServerApi.World.UnregisterGameTickListener(mListener);
    }
}