using HarmonyLib;
using MaltiezFSM.Additional;
using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Systems
{
    public class BasicBlock<TResistBehavior> : BaseSystem
        where TResistBehavior : EntityBehavior, ITempResistEntityBehavior
    {
        private readonly Dictionary<long, IResist> mResists = new();
        private readonly Dictionary<long, long> mCallbacks = new();
        private readonly Dictionary<string, BlockData> mParries = new();
        private readonly Dictionary<string, string> mSounds = new();
        private string mSoundSystemCode;
        private ISoundSystem mSoundSystem;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            mSoundSystemCode = definition["soundSystem"].AsString();

            foreach (JsonObject block in definition["blocks"].AsArray())
            {
                string blockCode = block["code"].AsString();
                mParries.Add(blockCode, new(block));
                mSounds.Add(blockCode, block["sound"].AsString());
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
                    if (mApi.Side != EnumAppSide.Server) return true;
                    string code = parameters["code"].AsString();
                    ScheduleBlock(player, code);
                    break;
                case "stop":
                    if (mApi.Side != EnumAppSide.Server) return true;
                    StopBlock(player);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicBlock] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }

        private void ScheduleBlock(EntityAgent player, string code)
        {
            long entityId = player.EntityId;
            if (mCallbacks.ContainsKey(entityId)) mApi.World.UnregisterCallback(mCallbacks[entityId]);
            if (mParries[code].delay > 0)
            {
                mCallbacks[entityId] = mApi.World.RegisterCallback(_ => StartBlock(player, code), mParries[code].delay);
            }
            else
            {
                StartBlock(player, code);
            }
        }

        private void StartBlock(EntityAgent player, string code)
        {
            if (mResists.ContainsKey(player.EntityId)) StopBlock(player);
            ITempResistEntityBehavior behavior = player.GetBehavior<TResistBehavior>();
            if (behavior == null)
            {
                mApi.Logger.Error("[FSMlib] [BasicBlock] No IResistEntityBehavior found");
                return;
            }
            IResist resist = mParries[code].AddToBehavior(behavior);
            if (mSounds[code] != null) resist.SetResistCallback((IResist resist, DamageSource damageSource, ref float damage, Entity receiver) => PlayBlockSound(receiver, mSounds[code]));
            mResists.Add(player.EntityId, resist);
        }

        private void StopBlock(EntityAgent player)
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

        private void PlayBlockSound(Entity player, string sound)
        {
            mSoundSystem?.PlaySound(sound, null, player as EntityAgent);
        }
    }

    internal class BlockData
    {
        public const int timeoutMs = 60000;
        
        public int delay;
        public float yawLeft;
        public float yawRight;
        public float pitchBottom;
        public float pitchTop;
        public Utils.DamageModifiers.TieredModifier damageModifier;

        public BlockData(JsonObject definition)
        {
            delay = definition["delay"].AsInt(0);
            damageModifier = Utils.DamageModifiers.GetTiered(definition["modifier"]);
            
            JsonObject direction = definition["direction"];
            yawLeft = direction["left"].AsFloat() * GameMath.DEG2RAD;
            yawRight = direction["right"].AsFloat() * GameMath.DEG2RAD;
            pitchBottom = direction["bottom"].AsFloat() * GameMath.DEG2RAD;
            pitchTop = direction["top"].AsFloat() * GameMath.DEG2RAD;
            
        }

        public IResist AddToBehavior(ITempResistEntityBehavior behavior)
        {
            BlockOrParryResist resist = new((pitchTop, pitchBottom, yawLeft, yawRight), false, new() { damageModifier });
            behavior.AddResist(resist, timeoutMs);
            return resist;
        }
    }
}
