using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable enable

namespace MaltiezFSM.Framework;

public sealed class StatusInputInvoker : IInputInvoker
{
    private const int cCheckInterval_ms = 33;
    private readonly ICoreClientAPI mClientApi;
    private readonly Dictionary<IStatusInput, IInputInvoker.InputCallback> mCallbacks = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();
    private readonly long mListener;
    private bool mDisposed = false;

    public StatusInputInvoker(ICoreClientAPI api)
    {
        mClientApi = api;
        mListener = mClientApi.World.RegisterGameTickListener(_ => CheckStatuses(), cCheckInterval_ms);
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
        foreach ((IStatusInput input, _) in mCallbacks)
        {
            if (CheckStatus(input) ^ input.Invert)
            {
                _ = HandleInput(input);
            }
        }
    }

    private bool CheckStatus(IStatusInput input)
    {
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
        Utils.SlotType slotType = input.Slot;

        IEnumerable<Utils.SlotData> slots = Utils.SlotData.GetForAllSlots(slotType, mCollectibles[input], mClientApi.World.Player);

        bool handled = false;
        foreach (Utils.SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input)))
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
