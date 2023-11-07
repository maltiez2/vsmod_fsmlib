using MaltiezFSM.Additional;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    public class BasicParry<TResistBehavior> : BaseSystem
        where TResistBehavior : EntityBehavior, IResistEntityBehavior
    {
        private readonly Dictionary<long, IResistance> mResists = new();
        private readonly Dictionary<long, long> mCallbacks = new();

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    int parryStart = parameters["parryWindowStart"].AsInt();
                    int parryEnd = parameters["parryWindowEnd"].AsInt();
                    ScheduleParry(player, parryStart, parryEnd);
                    break;
                case "stop":
                    StopParry(player);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicParry] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }

        private void ScheduleParry(EntityAgent player, int start, int finish)
        {
            long entityId = player.EntityId;
            if (mCallbacks.ContainsKey(entityId)) mApi.World.UnregisterCallback(mCallbacks[entityId]);
            mCallbacks[entityId] = mApi.World.RegisterCallback(_ => StartParry(player, finish - start), start);
        }
        
        private void StartParry(EntityAgent player, int timout)
        {
            if (mResists.ContainsKey(player.EntityId)) StopParry(player);
            IResistEntityBehavior behavior = player.GetBehavior<TResistBehavior>();
            SimpleParryResistance resist = new(behavior, mApi, timout);
            behavior.AddResist(resist);
            mResists.Add(player.EntityId, resist);
        }

        private void StopParry(EntityAgent player)
        {
            long entityId = player.EntityId;
            if (mCallbacks.ContainsKey(entityId))
            {
                mApi.World.UnregisterCallback(mCallbacks[entityId]);
                mCallbacks.Remove(entityId);
            }
            if (!mResists.ContainsKey(entityId)) return;
            IResistEntityBehavior behavior = player.GetBehavior<TResistBehavior>();
            behavior.RemoveResist(mResists[entityId]);
            mResists.Remove(entityId);
        }
    }
}
