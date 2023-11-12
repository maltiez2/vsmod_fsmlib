using MaltiezFSM.Framework;
using MaltiezFSM.Systems;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using static MaltiezFSM.Additional.ConstResistance;

namespace MaltiezFSM.Additional
{
    public abstract class BaseResistance : IResistance
    {
        protected DamageSourceCallback mCallback = null;

        public abstract bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver);

        public virtual void ResistCallback(DamageSource damageSource, ref float damage, Entity receiver, IResistEntityBehavior resistHolder)
        {
            if (mCallback != null) mCallback(this, damageSource, ref damage, receiver);
        }

        public virtual void SetResistCallback(DamageSourceCallback callback)
        {
            mCallback = callback;
        }
    }
    
    public class ConstResistance : BaseResistance, IConstResist
    {
        public delegate void DamageModifier(ref float damage);

        private readonly Dictionary<EnumDamageType, List<DamageModifier>> mResists = new();

        void IConstResist.Init(JsonObject[] resists, EntityBehavior behavior)
        {
            foreach (JsonObject resist in resists)
            {
                EnumDamageType damageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), resist["type"].AsString());
                mResists.Add(damageType, new List<DamageModifier>());
                foreach (JsonObject modifier in resist["modifiers"].AsArray())
                {
                    AttackDamageModifiers.AttackDamageModifier function = AttackDamageModifiers.Get(modifier["function"].AsString("Multiply"));
                    float value = modifier["function"].AsFloat(0);

                    mResists[damageType].Add((ref float damage) => damage = function(damage, value));
                }
            }
        }

        public override bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver)
        {
            if (!mResists.ContainsKey(damageSource.Type)) return false;

            foreach (DamageModifier resistModifier in mResists[damageSource.Type])
            {
                resistModifier(ref damage);
                if (damage < 0) damage = 0;
            }

            return true;
        }
    }


    public class ParryResistance : IResistance
    {
        private DamageSourceCallback mCallback = null;
        private readonly HashSet<EnumDamageType> mDamageTypes;
        private readonly ICoreAPI mApi;
        private readonly long mTimeoutTimer;
        private IResistEntityBehavior mReceiver;
        private long? mAttackerId = null;
        private int? mCapacity;

        public ParryResistance(IResistEntityBehavior receiver, ICoreAPI api, int timeout_ms, int? maxAttacksToBlock = null, HashSet<EnumDamageType> damageTypes = null)
        {
            mDamageTypes = damageTypes == null ? new() : damageTypes;
            mReceiver = receiver;
            mApi = api;

            mTimeoutTimer = api.World.RegisterCallback(_ => Stop(), timeout_ms);
            mCapacity = maxAttacksToBlock;
        }

        public void Stop()
        {
            mApi.World.UnregisterCallback(mTimeoutTimer);
            mReceiver.RemoveResist(this);
            mReceiver = null;
        }

        public bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver)
        {
            if (!mDamageTypes.Contains(damageSource.Type)) return false;

            long? attackerId = damageSource.SourceEntity != null ? damageSource.SourceEntity.EntityId : damageSource.CauseEntity?.EntityId;
            if (mAttackerId == null) mAttackerId = attackerId;
            if (mAttackerId != attackerId) return false;

            if (mCapacity == 0) return false;
            if (mCapacity != null) mCapacity -= 1;

            damage = 0;
            return true;
        }

        public void ResistCallback(DamageSource damageSource, ref float damage, Entity receiver, IResistEntityBehavior resistHolder)
        {
            if (mCallback != null) mCallback(this, damageSource, ref damage, receiver);
        }

        public void SetResistCallback(DamageSourceCallback callback)
        {
            mCallback = callback;
        }
    }

    public class SimpleBlockResistance : BaseResistance
    {
        private readonly HashSet<EnumDamageType> mDamageTypes;
        private readonly ICoreAPI mApi;
        private readonly long mTimeoutTimer;
        private IResistEntityBehavior mReceiver;
        private long? mAttackerId = null;
        private int? mCapacity;

        public SimpleBlockResistance(IResistEntityBehavior receiver, ICoreAPI api, int timeout_ms, int? maxAttacksToBlock = null, HashSet<EnumDamageType> damageTypes = null)
        {
            mDamageTypes = damageTypes == null ? new() : damageTypes;
            mReceiver = receiver;
            mApi = api;

            mTimeoutTimer = api.World.RegisterCallback(_ => Stop(), timeout_ms);
            mCapacity = maxAttacksToBlock;
        }

        public void Stop()
        {
            mApi.World.UnregisterCallback(mTimeoutTimer);
            mReceiver.RemoveResist(this);
            mReceiver = null;
        }

        public override bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver)
        {
            if (!mDamageTypes.Contains(damageSource.Type)) return false;

            long? attackerId = damageSource.SourceEntity != null ? damageSource.SourceEntity.EntityId : damageSource.CauseEntity?.EntityId;
            if (mAttackerId == null) mAttackerId = attackerId;
            if (mAttackerId != attackerId) return false;

            if (mCapacity == 0) return false;
            if (mCapacity != null) mCapacity -= 1;

            damage = 0;
            return true;
        }
    }
}
