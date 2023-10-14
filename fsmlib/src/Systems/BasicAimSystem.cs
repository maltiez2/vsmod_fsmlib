using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.Systems.IAimingSystem;

namespace MaltiezFSM.Systems
{
    public class BasicAim : BaseSystem, IAimingSystem
    {
        private static Random sRand = new Random();
        
        private float mDispersionMin;
        private float mDispersionMax;
        private long mAimTime_ms;
        private long mAimStartTime;
        private bool mIsAiming = false;
        private string mDescription;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            
            mDispersionMin = definition["dispersionMin_MOA"].AsFloat();
            mDispersionMax = definition["dispersionMax_MOA"].AsFloat();
            mAimTime_ms = definition["duration"].AsInt();
            mDescription = definition["description"].AsString();
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            
            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    mAimStartTime = mApi.World.ElapsedMilliseconds;
                    mIsAiming = true;
                    break;
                case "stop":
                    mIsAiming = false;
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicAim] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }

        DirectionOffset IAimingSystem.GetShootingDirectionOffset()
        {
            long currentTime = mApi.World.ElapsedMilliseconds;
            float aimProgress = mIsAiming ? Math.Clamp((float)(currentTime - mAimStartTime) / mAimTime_ms, 0, 1) : 0;
            float dispersion = mDispersionMax - (mDispersionMax - mDispersionMin) * aimProgress;
            float randomPitch = (float)(2 * (sRand.NextDouble() - 0.5) * (Math.PI / 180 / 60) * dispersion);
            float randomYaw = (float)(2 * (sRand.NextDouble() - 0.5) * (Math.PI / 180 / 60) * dispersion);
            return (randomPitch, randomYaw);
        }

        public override string[] GetDescription(ItemSlot slot, IWorldAccessor world)
        {
            if (mDescription == null) return null;

            return new string[]{ Lang.Get(mDescription, (float)mAimTime_ms / 1000, mDispersionMin, mDispersionMax) };
        }
    }
}
