using MaltiezFSM.API;
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
        if (CheckStatus(input) ^ input.InvertStatus)
        {
            _ = HandleInput(input);
        }
    }

    private bool CheckStatus(IStatusInput input)
    {
        if (mClientApi.World.Player.Entity == null || mClientApi.World.Player.Entity.World == null) return false;

        return input.Status switch
        {
            IStatusModifier.StatusType.Swimming => mClientApi.World.Player.Entity.Swimming,
            IStatusModifier.StatusType.OnFire => mClientApi.World.Player.Entity.IsOnFire,
            IStatusModifier.StatusType.Collided => mClientApi.World.Player.Entity.Collided,
            IStatusModifier.StatusType.CollidedHorizontally => mClientApi.World.Player.Entity.CollidedHorizontally,
            IStatusModifier.StatusType.CollidedVertically => mClientApi.World.Player.Entity.CollidedVertically,
            IStatusModifier.StatusType.EyesSubmerged => mClientApi.World.Player.Entity.IsEyesSubmerged(),
            IStatusModifier.StatusType.FeetInLiquid => mClientApi.World.Player.Entity.FeetInLiquid,
            IStatusModifier.StatusType.InLava => mClientApi.World.Player.Entity.InLava,
            IStatusModifier.StatusType.OnGround => mClientApi.World.Player.Entity.OnGround,
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
            if (CheckPlayerStatus(input, player) ^ input.InvertStatus)
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
                IStatusModifier.StatusType.Swimming => player.Entity.Swimming,
                IStatusModifier.StatusType.OnFire => player.Entity.IsOnFire,
                IStatusModifier.StatusType.Collided => player.Entity.Collided,
                IStatusModifier.StatusType.CollidedHorizontally => player.Entity.CollidedHorizontally,
                IStatusModifier.StatusType.CollidedVertically => player.Entity.CollidedVertically,
                IStatusModifier.StatusType.EyesSubmerged => player.Entity.IsEyesSubmerged(),
                IStatusModifier.StatusType.FeetInLiquid => player.Entity.FeetInLiquid,
                IStatusModifier.StatusType.InLava => player.Entity.InLava,
                IStatusModifier.StatusType.OnGround => player.Entity.OnGround,
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