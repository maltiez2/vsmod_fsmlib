using System.Collections.Generic;
using Vintagestory.API.Client;
using MaltiezFSM.API;
using Vintagestory.API.Common;
using System.Linq;

namespace MaltiezFSM.Framework
{
    public sealed class StatusInputManager : IInputInvoker
    {
        private const int cCheckInterval_ms = 33;
        private readonly ICoreClientAPI mClientApi;
        private readonly Dictionary<IStatusInput, IInputInvoker.InputCallback> mCallbacks = new();
        private readonly long mListener;
        private bool mDisposed = false;

        public StatusInputManager(ICoreClientAPI api)
        {
            mClientApi = api;
            mListener = mClientApi.World.RegisterGameTickListener(_ => CheckStatuses(), cCheckInterval_ms);
        }

        public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
        {
            if (input is IStatusInput statusInput) mCallbacks.Add(statusInput, callback);
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
            switch (input.GetStatusType())
            {
                case IStatusInput.StatusType.Activity:
                    return mClientApi.World.Player.Entity.IsActivityRunning(input.Activity);
                case IStatusInput.StatusType.Swimming:
                    return mClientApi.World.Player.Entity.Swimming;
                case IStatusInput.StatusType.OnFire:
                    return mClientApi.World.Player.Entity.IsOnFire;
                case IStatusInput.StatusType.Collided:
                    return mClientApi.World.Player.Entity.Collided;
                case IStatusInput.StatusType.CollidedHorizontally:
                    return mClientApi.World.Player.Entity.CollidedHorizontally;
                case IStatusInput.StatusType.CollidedVertically:
                    return mClientApi.World.Player.Entity.CollidedVertically;
                case IStatusInput.StatusType.EyesSubmerged:
                    return mClientApi.World.Player.Entity.IsEyesSubmerged();
                case IStatusInput.StatusType.FeetInLiquid:
                    return mClientApi.World.Player.Entity.FeetInLiquid;
                case IStatusInput.StatusType.InLava:
                    return mClientApi.World.Player.Entity.InLava;
                case IStatusInput.StatusType.OnGround:
                    return mClientApi.World.Player.Entity.OnGround;
            }

            return false;
        }


        private bool HandleInput(IStatusInput input)
        {
            Utils.SlotType slotType = input.SlotType();

            IEnumerable<Utils.SlotData> slots = Utils.SlotData.GetForAllSlots(slotType, mClientApi.World.Player);

            bool handled = false;
            foreach (Utils.SlotData slotData in slots.Where(slotData => mCallbacks[input](slotData, mClientApi.World.Player, input))) // Unreadable but now warning... I guess win win?
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
}
