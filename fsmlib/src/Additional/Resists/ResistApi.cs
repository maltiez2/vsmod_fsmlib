using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Additional
{
    public delegate void DamageSourceCallback(IResistance resist, DamageSource damageSource, ref float damage, Entity receiver);

    public interface IResistEntityBehavior
    {
        void AddResist(IResistance resist);
        bool RemoveResist(IResistance resist);
        void ClearResists();
    }

    public interface IResistance
    {
        bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver);
        void ResistCallback(DamageSource damageSource, ref float damage, Entity receiver, IResistEntityBehavior resistHolder);
        void SetResistCallback(DamageSourceCallback callback);
    }

    public interface IConstResist : IResistance
    {
        void Init(JsonObject[] resists, EntityBehavior behavior);
    }

    public interface IPersistentResistance : IResistance
    {
        byte[] Serialize();
        IPersistentResistance Deserialize(byte[] data);
        string GetType();
    }

    public interface IResistSerializer
    {
        Tuple<string, byte[]> Serialize(IPersistentResistance resist);
        IPersistentResistance Deserialize(Tuple<string, byte[]> data);
    }

    public interface IResistibleDamage
    {
        bool Bypass(IResistance resist, float damage);
        void ResistCallback(IResistance resist, DamageSource damageSource, ref float damage, Entity receiver);
    }
}
