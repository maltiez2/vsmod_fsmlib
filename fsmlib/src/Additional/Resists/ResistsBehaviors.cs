using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Additional
{
    public class EntityBehaviorConstResists<TResistType> : EntityBehavior
        where TResistType : IConstResist, new()
    {
        protected readonly Entity mEntity;
        protected readonly TResistType mResist = new();
        protected readonly string mName;

        protected EntityProperties mProperties;

        public EntityBehaviorConstResists(Entity entity) : base(entity)
        {
            mEntity = entity;
            mName = "fsmlibconstresists";
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            mProperties = properties;
            mResist.Init(attributes["resists"].AsArray(), this);
        }

        public override string PropertyName()
        {
            return mName;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if ((damageSource as IResistibleDamage)?.Bypass(mResist, damage) != true && mResist.ApplyResist(damageSource, ref damage, mEntity))
            {
                mResist.ResistCallback(damageSource, ref damage, mEntity, null);
                (damageSource as IResistibleDamage)?.ResistCallback(mResist, damageSource, ref damage, mEntity);
            }
        }
    }


    public class EntityBehaviorResists : EntityBehavior, ITempResistEntityBehavior
    {
        protected readonly Entity mEntity;
        protected readonly List<IResist> mResists = new();
        protected readonly string mName;

        protected EntityProperties mProperties;

        public EntityBehaviorResists(Entity entity) : base(entity)
        {
            mEntity = entity;
            mName = "fsmlibtempresists";
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            mProperties = properties;
        }

        public override string PropertyName()
        {
            return mName;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            foreach (IResist resist in mResists)
            {
                if ((damageSource as IResistibleDamage)?.Bypass(resist, damage) != true && resist.ApplyResist(damageSource, ref damage, mEntity))
                {
                    resist.ResistCallback(damageSource, ref damage, mEntity, this);                      
                    (damageSource as IResistibleDamage)?.ResistCallback(resist, damageSource, ref damage, mEntity);
                }
            }
            base.OnEntityReceiveDamage(damageSource, ref damage);
        }

        void IResistEntityBehavior.AddResist(IResist resist) => mResists.Add(resist);
        void IResistEntityBehavior.ClearResists() => mResists.Clear();
        bool IResistEntityBehavior.RemoveResist(IResist resist) => mResists.Remove(resist);
        void ITempResistEntityBehavior.AddResist(ITempResist resist, int? timeout_ms, int? attacksThreshold, float? damageThreshold)
        {
            mResists.Add(resist);
            resist?.Start(this, mEntity.World.Api, timeout_ms, attacksThreshold, attacksThreshold);
        }
    }
}
