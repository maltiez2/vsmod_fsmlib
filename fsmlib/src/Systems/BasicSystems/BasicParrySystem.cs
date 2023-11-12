using MaltiezFSM.Additional;
using MaltiezFSM.API;
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
        private readonly HashSet<EnumDamageType> mDamageTypes = new()
        {
            EnumDamageType.BluntAttack,
            EnumDamageType.PiercingAttack,
            EnumDamageType.SlashingAttack
        };
        private string mSoundSystemCode;
        private ISoundSystem mSoundSystem;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            mSoundSystemCode = definition["soundSystem"].AsString();
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
                    int parryStart = parameters["parryWindowStart"].AsInt();
                    int parryEnd = parameters["parryWindowEnd"].AsInt();
                    string sound = parameters["sound"].AsString();
                    ScheduleParry(player, parryStart, parryEnd, sound);
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

        private void ScheduleParry(EntityAgent player, int start, int finish, string sound)
        {
            long entityId = player.EntityId;
            if (mCallbacks.ContainsKey(entityId)) mApi.World.UnregisterCallback(mCallbacks[entityId]);
            mCallbacks[entityId] = mApi.World.RegisterCallback(_ => StartParry(player, finish - start, sound), start);
        }
        
        private void StartParry(EntityAgent player, int timout, string sound)
        {
            if (mResists.ContainsKey(player.EntityId)) StopParry(player);
            IResistEntityBehavior behavior = player.GetBehavior<TResistBehavior>();
            if (behavior == null)
            {
                mApi.Logger.Error("[FSMlib] [BasicParry] No IResistEntityBehavior found");
                return;
            }
            SimpleParryResistance resist = new(behavior, mApi, timout, null, mDamageTypes);
            if (sound != null) resist.SetResistCallback((IResistance resist, DamageSource damageSource, ref float damage, Entity receiver) => PlayParrySound(receiver, sound));
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

        private void PlayParrySound(Entity player, string sound)
        {
            mSoundSystem?.PlaySound(sound, null, player as EntityAgent);
        }
    }
}
