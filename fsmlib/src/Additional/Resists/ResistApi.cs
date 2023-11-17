using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.Framework;

namespace MaltiezFSM.Additional
{
    public delegate void DamageSourceCallback(IResist resist, DamageSource damageSource, ref float damage, Entity receiver);

    public interface IResistEntityBehavior
    {
        void AddResist(IResist resist);
        bool RemoveResist(IResist resist);
        void ClearResists();
    }

    public interface ITempResistEntityBehavior : IResistEntityBehavior
    {
        void AddResist(ITempResist resist, int? timeout_ms = null, int? attacksThreshold = null, float? damageThreshold = null);
    }

    public interface IResist
    {
        bool ApplyResist(DamageSource damageSource, ref float damage, Entity receiver);
        void ResistCallback(DamageSource damageSource, ref float damage, Entity receiver, IResistEntityBehavior resistHolder);
        void SetResistCallback(DamageSourceCallback callback);
    }

    public interface IConstResist : IResist
    {
        void Init(JsonObject[] resists, EntityBehavior behavior);
    }

    public interface ITempResist : IResist
    {
        void Start(IResistEntityBehavior behavior, ICoreAPI api = null, int? timeout_ms = null, int? attacksThreshold = null, float? damageThreshold = null);
        void Stop();
    }

    public interface IDirectionalResist : IResist
    {
        void SetConstrains(Utils.DirectionConstrain constrain, bool horizontal = true);
        bool Check(Entity receiver, Entity source);
    }

    public interface IPersistentResistance : IResist
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
        bool Bypass(IResist resist, float damage);
        void ResistCallback(IResist resist, DamageSource damageSource, ref float damage, Entity receiver);
    }
}
