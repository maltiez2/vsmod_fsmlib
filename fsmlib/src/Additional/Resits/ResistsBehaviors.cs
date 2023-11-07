using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace MaltiezFSM.Additional
{
    public class EntityBehaviorConstResists<TResistType> : EntityBehavior
        where TResistType : IConstResist, new()
    {
        protected readonly Entity mEntity;
        protected readonly TResistType mResist = new();
        protected readonly string mName;

        protected EntityProperties mProperties;

        public EntityBehaviorConstResists(Entity entity, string code = "0") : base(entity)
        {
            mEntity = entity;
            mName = "fsmlibconstresists-" + code;
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


    public class EntityBehaviorResists : EntityBehavior, IResistEntityBehavior
    {
        protected readonly Entity mEntity;
        protected readonly List<IResistance> mResists = new();
        protected readonly string mName;
        protected readonly IResistSerializer mSerializer;

        protected EntityProperties mProperties;

        public EntityBehaviorResists(IResistSerializer serializer, Entity entity, string code = "0") : base(entity)
        {
            mEntity = entity;
            mSerializer = serializer;
            mName = "fsmlibresists-" + code;
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
            foreach (IResistance resist in mResists)
            {
                if ((damageSource as IResistibleDamage)?.Bypass(resist, damage) != true && resist.ApplyResist(damageSource, ref damage, mEntity))
                {
                    resist.ResistCallback(damageSource, ref damage, mEntity, this);
                    (damageSource as IResistibleDamage)?.ResistCallback(resist, damageSource, ref damage, mEntity);
                }
            }
        }

        void IResistEntityBehavior.AddResist(IResistance resist) => mResists.Add(resist);
        void IResistEntityBehavior.ClearResists() => mResists.Clear();
        bool IResistEntityBehavior.RemoveResist(IResistance resist) => mResists.Remove(resist);

        public override void OnEntityLoaded()
        {
            // @TODO add serialisation of persistant resists in 1.19
        }
    }
}
