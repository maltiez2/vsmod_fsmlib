using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using MaltiezFSM.API;
using MaltiezFSM.Systems;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework
{
    public class StatusInputManager : IStatusInputManager // @TODO move to server side
    {
        public const int CheckInterval_ms = 30;
        
        private readonly ICoreClientAPI mClientApi;
        private readonly Dictionary<IStatusInput.StatusType, List<IStatusInputManager.InputCallback>> mCallbacks = new();

        public StatusInputManager(ICoreClientAPI api)
        {
            mClientApi = api;
            mClientApi.World.RegisterGameTickListener(_ => CheckStatuses(), CheckInterval_ms);
        }

        void IStatusInputManager.RegisterStatusInput(IStatusInput input, IStatusInputManager.InputCallback callback)
        {
            IStatusInput.StatusType statusType = input.GetStatusType();
            if (!mCallbacks.ContainsKey(statusType)) mCallbacks.Add(statusType, new());
            mCallbacks[statusType].Add(callback);
        }

        void CheckStatuses()
        {
            foreach ((IStatusInput.StatusType statusType, List<IStatusInputManager.InputCallback> callbacks) in mCallbacks)
            {
                if (CheckStatus(statusType))
                {
                    foreach (IStatusInputManager.InputCallback callback in callbacks)
                    {
                        callback(statusType);
                    }
                }
            }
        }

        bool CheckStatus(IStatusInput.StatusType status)
        {
            switch (status)
            {
                case IStatusInput.StatusType.SWIMMING:
                    return mClientApi.World.Player.Entity.Swimming;
            }

            return false;
        }
    }
}
