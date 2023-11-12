using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using System.Linq;


namespace MaltiezFSM.Systems
{
    public class SimpleMelee : BaseSystem
    {
        private readonly Dictionary<string, SimpleMeleeAttackData> mAttacks = new();
        private string mAnimationSystemCode;
        private string mSoundSystemCode;
        private readonly Dictionary<long, Utils.TickBasedTimer> mTimers = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mAnimationSystemCode = definition["animationSystem"].AsString();
            mSoundSystemCode = definition["soundSystem"].AsString();

            foreach (JsonObject attack in definition["attacks"].AsArray())
            {
                mAttacks.Add(attack["code"].AsString(), new(attack, api));
            }
        }

        public override void SetSystems(Dictionary<string, ISystem> systems)
        {
            ITranformAnimationSystem animationsSystem = systems[mAnimationSystemCode] as ITranformAnimationSystem;
            ISoundSystem soundSystem = systems[mSoundSystemCode] as ISoundSystem;

            foreach ((_, SimpleMeleeAttackData attack) in mAttacks)
            {
                attack.SetSystems(soundSystem, animationsSystem);
            }
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            long playerId = player.EntityId;
            if (!mTimers.ContainsKey(playerId)) mTimers.Add(playerId, null);
            mTimers[playerId]?.Stop();

            string action = parameters["action"].AsString();
            string attack = parameters["attack"].AsString();

            if (!mAttacks.ContainsKey(attack))
            {
                mApi.Logger.Error("[FSMlib] [SimpleMelee] [Process] Attack does not exists: {0}", attack);
                return false;
            }

            switch (action)
            {
                case "start":
                    if (mApi is ICoreServerAPI)
                    {
                        player.Attributes.SetInt("didattack", 0);
                        mTimers[playerId] = new(mApi, mAttacks[attack].duration, (float progress) => mAttacks[attack].TryAttack(player, mApi as ICoreServerAPI, progress));
                    }
                    mAttacks[attack].StartAnimation(slot, player);
                    break;
                case "stop":
                    if (mApi is ICoreServerAPI)
                    {
                        mTimers[playerId]?.Stop();
                    }
                    mAttacks[attack].StopAnimation(slot, player);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [SimpleMelee] [Process] Action does not exists: {0}", action);
                    return false;
            }
            return true;
        }
    }

    public class SimpleMeleeAttackData
    {
        public int duration { get => mDuration; }
        
        private readonly int mDuration;
        private readonly Tuple<int, int> mHitWindow;
        private readonly List<AttackDamageType> mDamageTypes = new();
        private readonly AttackAnimationPlayer mAnimationPlayer;
        private readonly ICoreAPI mApi;


        public SimpleMeleeAttackData(JsonObject definition, ICoreAPI api)
        {
            mApi = api;
            mDuration = definition["duration"].AsInt();
            mHitWindow = new(definition["hitWindowStart_ms"].AsInt(), definition["hitWindowEnd_ms"].AsInt());
            mAnimationPlayer = new(definition);

            foreach (JsonObject damageType in definition["damageTypes"].AsArray())
            {
                mDamageTypes.Add(new(damageType, api));
            }
        }

        public void SetSystems(ISoundSystem soundSystem, ITranformAnimationSystem animationSystem)
        {
            mAnimationPlayer.SetAnimationSystem(animationSystem);

            foreach (AttackDamageType damageType in mDamageTypes)
            {
                damageType.SetSoundSystem(soundSystem);
            }
        }
        public bool TryAttack(EntityAgent player, ICoreServerAPI api, float attackProgress)
        {
            if (!(mHitWindow.Item1 < attackProgress * mDuration && attackProgress * mDuration < mHitWindow.Item2)) return false;
            
            if (player.Attributes.GetInt("didattack") == 0 && Attack(player, api))
            {
                player.Attributes.SetInt("didattack", 1);
                return true;
            }

            return false;
        }
        public void StartAnimation(ItemSlot slot, EntityAgent player)
        {
            mAnimationPlayer.Start(slot, player);
        }
        public void StopAnimation(ItemSlot slot, EntityAgent player)
        {
            mAnimationPlayer.Stop(slot, player);
        }

        private bool Attack(EntityAgent player, ICoreServerAPI api)
        {
            if (player == null) return false;

            EntitySelection selection = (player as EntityPlayer)?.EntitySelection;

            if (selection == null) return false;

            Entity target = selection.Entity;

            if (!CheckIfCanAttack(player, target, api)) return false;

            float distance = GetDistance(player as EntityPlayer, target);

            bool successfullyHit = false;

            foreach (AttackDamageType damageType in mDamageTypes.Where(x => x.Attack(player, target, distance)))
            {
                successfullyHit = true;
            }

            return successfullyHit;
        }
        static private bool CheckIfCanAttack(EntityAgent attacker, Entity target, ICoreServerAPI api)
        {
            IServerPlayer fromPlayer = null;
            if (attacker is EntityPlayer)
            {
                fromPlayer = (attacker as EntityPlayer).Player as IServerPlayer;
            }

            if (fromPlayer != null)
            {
                if (target is EntityPlayer && (!api.Server.Config.AllowPvP || !fromPlayer.HasPrivilege("attackplayers"))) return false;
                if (target is EntityAgent && !fromPlayer.HasPrivilege("attackcreatures")) return false;
            }

            return true;
        }
        static private float GetDistance(EntityPlayer attacker, Entity target)
        {
            Cuboidd hitbox = target.SelectionBox.ToDouble().Translate(target.Pos.X, target.Pos.Y, target.Pos.Z);
            EntityPos sidedPos = attacker.SidedPos;
            double x = sidedPos.X + attacker.LocalEyePos.X;
            double y = sidedPos.Y + attacker.LocalEyePos.Y;
            double z = sidedPos.Z + attacker.LocalEyePos.Z;
            return (float)hitbox.ShortestDistanceFrom(x, y, z);
        }
    }

    static public class AttackDamageModifiers
    {
        public delegate float AttackDamageModifier(float damage, float stat);
        public readonly static AttackDamageModifier Multiply = (float damage, float stat) => { return damage * stat; };
        public readonly static AttackDamageModifier Divide = (float damage, float stat) => { return stat == 0 ? damage : damage / stat; };
        public readonly static AttackDamageModifier Add = (float damage, float stat) => { return damage + stat; };
        public readonly static AttackDamageModifier Subtract = (float damage, float stat) => { return damage - stat; };

        public static AttackDamageModifier Get(string name)
        {
            switch (name)
            {
                case "Multiply": return Multiply;
                case "Divide": return Divide;
                case "Add": return Add;
                case "Subtract": return Subtract;
                default: throw new NotImplementedException();
            }
        }
    }

    public class AttackDamageType
    {
        private readonly float mDamage;
        private readonly float mKnockback;
        private readonly string mSound;
        private readonly EnumDamageType mDamageType;
        private readonly Tuple<float, float> mReachWindow;
        private readonly List<Tuple<string, AttackDamageModifiers.AttackDamageModifier>> mModifiers = new();
        private readonly ICoreAPI mApi;

        private ISoundSystem mSoundSystem;

        public AttackDamageType(JsonObject definition, ICoreAPI api)
        {
            mApi = api;

            mSound = definition["sound"].AsString();
            mDamage = definition["damage"].AsFloat();
            mKnockback = definition["knockback"].AsFloat(0);
            if (definition.KeyExists("minReach") || definition.KeyExists("maxReach"))
            {
                mReachWindow = new(definition["minReach"].AsFloat(0), definition["maxReach"].AsFloat(1));
            }
            else
            {
                mReachWindow = new(0, definition["reach"].AsFloat(1));
            }
            
            mDamageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), definition["type"].AsString("PiercingAttack"));

            if (definition.KeyExists("stats"))
            {
                foreach (JsonObject stat in definition["stats"].AsArray())
                {
                    mModifiers.Add(new(stat["code"].AsString(), AttackDamageModifiers.Get(stat["type"].AsString("Multiply"))));
                }
            }
        }

        public void SetSoundSystem(ISoundSystem system) => mSoundSystem = system;

        public bool Attack(EntityAgent attacker, Entity target, float distance)
        {
            if (!(mReachWindow.Item1 < distance && distance < mReachWindow.Item2)) return false;

            float damage = mDamage;

            foreach ((string stat, var modifier) in mModifiers)
            {
                damage = modifier(damage, attacker.Stats.GetBlended(stat));
            }

            bool damageReceived = target.ReceiveDamage(new DamageSource()
            {
                Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
                SourceEntity = null,
                CauseEntity = attacker,
                Type = mDamageType
            }, damage);

            if (damageReceived)
            {
                Vec3f knockback = (target.Pos.XYZFloat - attacker.Pos.XYZFloat).Normalize() * mKnockback / 10f * target.Properties.KnockbackResistance;
                target.SidedPos.Motion.Add(knockback);

                if (mSound != null) mSoundSystem.PlaySound(mSound, null, attacker);
            }

            return damageReceived;
        }
    }

    public class AttackAnimationPlayer
    {
        private readonly List<ITranformAnimationSystem.AnimationData> mAnimations = new();
        private readonly Dictionary<long, int?> mAnimationIndex = new();
        private readonly int mEaseOut;
        private readonly bool mEaseOutOnFinish;
        private ITranformAnimationSystem mAnimationsSystem;

        public AttackAnimationPlayer(JsonObject definition)
        {
            mEaseOut = definition["easeOut"].AsInt();
            mEaseOutOnFinish = definition["easeOutOnFinish"].AsBool(true);

            foreach (JsonObject animation in definition["animations"].AsArray())
            {
                mAnimations.Add(new(animation));
            }
        }

        public void SetAnimationSystem(ITranformAnimationSystem animationSystem) => mAnimationsSystem = animationSystem;

        public void Start(ItemSlot slot, EntityAgent player)
        {
            long playerId = player.EntityId;

            if (mAnimationIndex.ContainsKey(playerId) && mAnimationIndex[playerId] != null)
            {
                //mAnimationsSystem.PlayAnimation(slot, player, mAnimations[(int)mAnimationIndex[playerId]], null, "cancel");
            }

            if (!mAnimationIndex.ContainsKey(playerId)) mAnimationIndex[playerId] = 0;
            mAnimationIndex[playerId] = 0;

            mAnimationsSystem.PlayAnimation(slot, player, mAnimations[0], () => Callback(slot, player, 1));
        }

        public void Stop(ItemSlot slot, EntityAgent player)
        {
            PlayAnimationBack(slot, player);
        }

        private void Callback(ItemSlot slot, EntityAgent player, int nextIndex)
        {
            long playerId = player.EntityId;

            if (!mAnimationIndex.ContainsKey(playerId) || mAnimationIndex[playerId] == null)
            {
                return;
            }

            if (nextIndex >= mAnimations.Count)
            {
                if (mEaseOutOnFinish) PlayAnimationBack(slot, player);
                return;
            }

            mAnimationIndex[playerId] = nextIndex;
            mAnimationsSystem.PlayAnimation(slot, player, mAnimations[nextIndex], () => Callback(slot, player, nextIndex + 1));
        }

        private void PlayAnimationBack(ItemSlot slot, EntityAgent player)
        {
            long playerId = player.EntityId;
            if (mAnimationIndex.ContainsKey(playerId) && mAnimationIndex[playerId] != null)
            {
                var animation = mAnimations[(int)mAnimationIndex[playerId]];
                int? duration = animation.duration;
                animation.duration = mEaseOut;
                mAnimationsSystem.PlayAnimation(slot, player, animation, null, "backward");
                mAnimationIndex[playerId] = null;
                animation.duration = duration;
            }
        }
    }
}
