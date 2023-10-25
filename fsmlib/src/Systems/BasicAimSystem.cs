using System;
using System.Reflection.Metadata;
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
        private bool mIsAiming = false;
        private string mDescription;
        private string mTimeAttrName;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            
            mDispersionMin = definition["dispersionMin_MOA"].AsFloat();
            mDispersionMax = definition["dispersionMax_MOA"].AsFloat();
            mAimTime_ms = definition["duration"].AsInt();
            mDescription = definition["description"].AsString();
            mTimeAttrName = "FSM." + code + ".timePassed";
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            
            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    WriteStartTimeTo(slot, mApi.World.ElapsedMilliseconds);
                    mIsAiming = true;
                    break;
                case "stop":
                    WriteStartTimeTo(slot, 0);
                    mIsAiming = false;
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicAim] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }

        DirectionOffset IAimingSystem.GetShootingDirectionOffset(ItemSlot slot, EntityAgent player)
        {
            long currentTime = mApi.World.ElapsedMilliseconds;
            float aimProgress = mIsAiming ? Math.Clamp((float)(currentTime - ReadStartTimeFrom(slot)) / mAimTime_ms, 0, 1) : 0;
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

        private void WriteStartTimeTo(ItemSlot slot, long time)
        {
            slot?.Itemstack?.Attributes.SetLong(mTimeAttrName, time);
            slot?.MarkDirty();
        }
        private long ReadStartTimeFrom(ItemSlot slot)
        {
            long? startTime = slot?.Itemstack?.Attributes?.GetLong(mTimeAttrName, 0);
            return startTime == null || startTime == 0 ? mApi.World.ElapsedMilliseconds : startTime.Value;
        }
    }
}
