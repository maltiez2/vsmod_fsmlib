using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Additional
{
    public abstract class BaseResist : IResist
    {
        protected DamageSourceCallback mCallback = null;
        protected Dictionary<EnumDamageType, List<Utils.DamageModifiers.TieredModifier>> mResists = new();

        public virtual bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver)
        {
            if (!mResists.ContainsKey(damageSource.Type)) return false;

            foreach (Utils.DamageModifiers.TieredModifier resistModifier in mResists[damageSource.Type])
            {
                resistModifier(ref damage, ref damageSource.DamageTier);
                if (damage < 0) damage = 0;
            }

            return true;
        }

        public virtual void ResistCallback(DamageSource damageSource, ref float damage, Entity receiver, IResistEntityBehavior resistHolder)
        {
            if (mCallback != null) mCallback(this, damageSource, ref damage, receiver);
        }

        public virtual void SetResistCallback(DamageSourceCallback callback)
        {
            mCallback = callback;
        }

        protected void FillResistsFrom(JsonObject[] resists)
        {
            mResists.Clear();

            foreach (JsonObject resist in resists)
            {
                EnumDamageType damageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), resist["type"].AsString());
                mResists.Add(damageType, new());
                foreach (JsonObject modifier in resist["modifiers"].AsArray())
                {
                    Utils.DamageModifiers.TieredModifier function = Utils.DamageModifiers.GetTiered(modifier);

                    mResists[damageType].Add((ref float damage, ref int tier) => function(ref damage, ref tier));
                }
            }
        }

        protected void SetResist(EnumDamageType damageType, params Utils.DamageModifiers.TieredModifier[] modifiers)
        {
            if (mResists.ContainsKey(damageType))
            {
                mResists[damageType].Clear();
            }
            else
            {
                mResists[damageType] = new();
            }

            foreach (Utils.DamageModifiers.TieredModifier modifier in modifiers)
            {
                mResists[damageType].Add(modifier);
            }
        }

        protected void SetResist(EnumDamageType damageType, List<Utils.DamageModifiers.TieredModifier> modifiers)
        {
            if (mResists.ContainsKey(damageType))
            {
                mResists[damageType].Clear();
            }
            else
            {
                mResists[damageType] = new();
            }

            foreach (Utils.DamageModifiers.TieredModifier modifier in modifiers)
            {
                mResists[damageType].Add(modifier);
            }
        }
    }
    
    public class ConstResist : BaseResist, IConstResist
    {
        protected EntityBehavior mBehavior;

        void IConstResist.Init(JsonObject[] resists, EntityBehavior behavior)
        {
            mBehavior = behavior;
            FillResistsFrom(resists);
        }
    }

    public class TempResist : BaseResist, ITempResist
    {
        private IResistEntityBehavior mResistBehavior;
        private ICoreAPI mApi;
        private long? mTimeoutTimer;
        private int? mAttacksThreshold;
        private float? mDamageThreshold;

        void ITempResist.Start(IResistEntityBehavior behavior, ICoreAPI api, int? timeout_ms, int? attacksThreshold, float? damageThreshold)
        {
            mResistBehavior = behavior;
            mApi = api;

            StartTimer(timeout_ms);
            SetAttacksThreshold(attacksThreshold);
            SetDamageThreshold(damageThreshold);
        }

        void ITempResist.Stop()
        {
            Stop();
        }

        public override void ResistCallback(DamageSource damageSource, ref float damage, Entity receiver, IResistEntityBehavior resistHolder)
        {
            base.ResistCallback(damageSource, ref damage, receiver, resistHolder);

            if (!CheckAttacksThreshold()) Stop();
            if (!CheckDamageThreshold(damage)) Stop();
        }

        protected virtual void Stop()
        {
            StopTimer();
            mResistBehavior?.RemoveResist(this);
            mResistBehavior = null;
        }

        private void StartTimer(int? timeout_ms)
        {
            if (mApi == null || timeout_ms == null) return;

            mTimeoutTimer = mApi.World.RegisterCallback(_ => Stop(), (int)timeout_ms);
        }
        private void StopTimer()
        {
            if (mTimeoutTimer != null)
            {
                mApi?.World.UnregisterCallback((long)mTimeoutTimer);
                mTimeoutTimer = null;
                mApi = null;
            }
        }

        private void SetAttacksThreshold(int? attacksThreshold)
        {
            mAttacksThreshold = attacksThreshold;
        }
        private bool CheckAttacksThreshold()
        {
            if (mAttacksThreshold == null) return true;
            mAttacksThreshold -= 1;
            return mAttacksThreshold > 0;
        }

        private void SetDamageThreshold(float? damageThreshold)
        {
            mDamageThreshold = damageThreshold;
        }
        private bool CheckDamageThreshold(float damage)
        {
            if (mDamageThreshold == null) return true;
            mDamageThreshold -= damage;
            return mDamageThreshold > 0;
        }
    }

    public class DirectionalResist : TempResist, IDirectionalResist
    {
        protected Utils.DirectionConstrain mConstrain = null;
        protected bool mHorizontal = true;

        public override bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver)
        {
            Entity source = damageSource.SourceEntity ?? damageSource.CauseEntity;

            if (!CheckDirection(receiver, source, mHorizontal)) return false;

            return base.ApplyResist(damageSource, ref damage, receiver);
        }

        protected virtual bool CheckDirection(Entity receiver, Entity source, bool horizontal = true)
        {
            if (mConstrain == null) return true;
            
            EntityPlayer player = receiver as EntityPlayer;
            if (player == null || source == null) return false;

            Vec3f sourceEyesPosition = source.ServerPos.XYZFloat.Add(0, (float)source.LocalEyePos.Y, 0);
            Vec3f playerEyesPosition = player.ServerPos.XYZFloat.Add(0, (float)player.LocalEyePos.Y, 0);
            Vec3f attackDirection = sourceEyesPosition - playerEyesPosition;
            Vec3f playerViewDirection = EntityPos.GetViewVector(player.SidedPos.Pitch, player.SidedPos.Yaw);
            if (horizontal) playerViewDirection.Y = 0;

            var direction = Utils.ToReferenceFrame(playerViewDirection, attackDirection);
            Utils.DirectionOffset offset = new(direction, new Vec3f(0, 0, 1));

            return mConstrain.Check(offset);
        }

        bool IDirectionalResist.Check(Entity receiver, Entity source)
        {
            return CheckDirection(receiver, source, mHorizontal);
        }

        void IDirectionalResist.SetConstrains(Utils.DirectionConstrain constrain, bool horizontal)
        {
            mConstrain = constrain;
            mHorizontal = horizontal;
        }
    }

    public class BlockOrParryResist : DirectionalResist
    {
        static readonly public ImmutableHashSet<EnumDamageType> defaultDamageTypes = ImmutableHashSet.Create(
            EnumDamageType.BluntAttack,
            EnumDamageType.SlashingAttack,
            EnumDamageType.PiercingAttack
        );
        static readonly public Utils.DamageModifiers.TieredModifier defaultModifier = (ref float damage, ref int tier) => damage = 0;

        public BlockOrParryResist(Utils.DirectionConstrain constrain = null, bool horizontalConstrain = false, List<Utils.DamageModifiers.TieredModifier> modifiers = null, ImmutableHashSet<EnumDamageType> damageTypes = null)
        {
            ImmutableHashSet<EnumDamageType> damageTypesToAdd = damageTypes == null ? defaultDamageTypes : damageTypes;
            List<Utils.DamageModifiers.TieredModifier> modifiersToAdd = modifiers == null ? new() { defaultModifier } : modifiers;

            foreach (EnumDamageType damageType in damageTypesToAdd)
            {
                SetResist(damageType, modifiersToAdd);
            }

            mConstrain = constrain;
            mHorizontal = horizontalConstrain;
        }
    }
}
