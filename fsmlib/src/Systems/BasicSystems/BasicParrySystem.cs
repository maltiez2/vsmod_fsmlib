using MaltiezFSM.Additional;
using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Systems
{
    public class BasicParry<TResistBehavior> : BaseSystem
        where TResistBehavior : EntityBehavior, ITempResistEntityBehavior
    {
        private readonly Dictionary<long, IResist> mResists = new();
        private readonly Dictionary<long, long> mCallbacks = new();
        private readonly Dictionary<string, ParryData> mParries = new();
        private readonly Dictionary<string, string> mSounds = new();
        private string mSoundSystemCode;
        private ISoundSystem mSoundSystem;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            mSoundSystemCode = definition["soundSystem"].AsString();

            foreach (JsonObject parry in definition["parries"].AsArray())
            {
                string parryCode = parry["code"].AsString();
                mParries.Add(parryCode, new(parry));
                mSounds.Add(parryCode, parry["sound"].AsString());
            }
        }

        public override void SetSystems(Dictionary<string, ISystem> systems)
        {
            if (systems.ContainsKey(mSoundSystemCode)) mSoundSystem = systems[mSoundSystemCode] as ISoundSystem;
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    string code = parameters["code"].AsString();
                    ScheduleParry(player, code);
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

        private void ScheduleParry(EntityAgent player, string code)
        {
            long entityId = player.EntityId;
            if (mCallbacks.ContainsKey(entityId)) mApi.World.UnregisterCallback(mCallbacks[entityId]);
            mCallbacks[entityId] = mApi.World.RegisterCallback(_ => StartParry(player, code), mParries[code].windowStart);
        }
        
        private void StartParry(EntityAgent player, string code)
        {
            if (mResists.ContainsKey(player.EntityId)) StopParry(player);
            ITempResistEntityBehavior behavior = player.GetBehavior<TResistBehavior>();
            if (behavior == null)
            {
                mApi.Logger.Error("[FSMlib] [BasicParry] No IResistEntityBehavior found");
                return;
            }
            IResist resist = mParries[code].AddToBehavior(behavior);
            if (mSounds[code] != null) resist.SetResistCallback((IResist resist, DamageSource damageSource, ref float damage, Entity receiver) => PlayParrySound(receiver, mSounds[code]));
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

        private void PlayParrySound(Entity player, string sound)
        {
            mSoundSystem?.PlaySound(sound, null, player as EntityAgent);
        }
    }

    internal class ParryData
    {
        public int windowStart;
        public int windowEnd;
        public float yawLeft;
        public float yawRight;
        public float pitchBottom;
        public float pitchTop;

        public ParryData(JsonObject definition)
        {
            JsonObject window = definition["window"];
            JsonObject direction = definition["direction"];
            windowStart = window["start"].AsInt();
            windowEnd = window["end"].AsInt();
            yawLeft = direction["left"].AsFloat() * GameMath.DEG2RAD;
            yawRight = direction["right"].AsFloat() * GameMath.DEG2RAD;
            pitchBottom = direction["bottom"].AsFloat() * GameMath.DEG2RAD;
            pitchTop = direction["top"].AsFloat() * GameMath.DEG2RAD;
        }

        public IResist AddToBehavior(ITempResistEntityBehavior behavior)
        {
            BlockOrParryResist resist = new((pitchTop, pitchBottom, yawLeft, yawRight));
            behavior.AddResist(resist, windowEnd - windowStart);
            return resist;
        }
    }
}
