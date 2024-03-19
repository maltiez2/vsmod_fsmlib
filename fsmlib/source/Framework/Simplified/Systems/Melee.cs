using ProtoBuf;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework.Simplified.Systems;

public sealed class MeleeClient : BaseSystem
{
    public MeleeClient(ICoreClientAPI api, string networkId, string debugName = "") : base(api, debugName)
    {
        _api = api;
    }

    public long Start(IPlayer player, MeleeAttack attack, ItemSlot slot, System.Func<MeleeAttack.Result, bool> callback, bool rightHand = true)
    {
        attack.Start(player);
        long id = _nextId++;
        long timer = _api.World.RegisterGameTickListener(dt => Step(dt, attack, player, slot, rightHand, callback, id), 0);
        _timers[id] = timer;

        return id;
    }

    public void Stop(long id)
    {
        if (_timers.ContainsKey(id)) _api.World.UnregisterCallback(_timers[id]);
    }


    private long _nextId = 0;
    private readonly ICoreClientAPI _api;
    private readonly Dictionary<long, long> _timers = new();

    private void Step(float dt, MeleeAttack attack, IPlayer player, ItemSlot slot, bool rightHand, System.Func<MeleeAttack.Result, bool> callback, long id)
    {
        MeleeAttack.Result result = attack.Step(player, dt, slot, Synchronise, rightHand);

        if (result == MeleeAttack.Result.None) return;

        if (callback.Invoke(result) || result == MeleeAttack.Result.Finished)
        {
            Stop(id);
        }
    }
    private void Synchronise(List<MeleeAttackDamagePacket> packets)
    {

    }
}
public sealed class MeleeServer : BaseSystem
{
    public MeleeServer(ICoreServerAPI api, string networkId, string debugName = "") : base(api, debugName)
    {
        _api = api;
    }

    private readonly ICoreServerAPI _api;
}

public sealed class MeleeAttack
{
    public enum Result
    {
        None,
        HitEntity,
        HitTerrain,
        Finished
    }

    public HitWindow Window { get; }
    public IEnumerable<MeleeAttackDamageType> DamageTypes { get; }
    public StatsModifier? DurationModifier { get; }
    public float MaxReach { get; }

    public MeleeAttack(ICoreClientAPI api, HitWindow hitWindow, IEnumerable<MeleeAttackDamageType> damageTypes, float maxReach, StatsModifier? durationModifier = null)
    {
        _api = api;
        Window = hitWindow;
        DamageTypes = damageTypes;
        DurationModifier = durationModifier;
        MaxReach = maxReach;
    }

    public void Start(IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        _currentTime[entityId] = 0;
        _totalTime[entityId] = DurationModifier?.Calc(player, (float)Window.End.TotalMilliseconds) ?? (float)Window.End.TotalMilliseconds;
        if (_totalTime[entityId] <= 0) _totalTime[entityId] = 1;

        if (_attackedEntities.ContainsKey(entityId))
        {
            _attackedEntities[entityId].Clear();
        }
        else
        {
            _attackedEntities[entityId] = new();
        }

    }
    public Result Step(IPlayer player, float dt, ItemSlot slot, Action<List<MeleeAttackDamagePacket>> networkCallback, bool rightHand = true)
    {
        _currentTime[player.Entity.EntityId] += dt;
        float progress = GameMath.Clamp(_currentTime[player.Entity.EntityId] / _totalTime[player.Entity.EntityId], 0, 1);
        if (!Window.Check(progress)) return Result.Finished;

        bool success = LineSegmentCollider.Transform(DamageTypes, player.Entity, slot, _api, rightHand);
        if (!success) return Result.None;

        if (CheckTerrainCollision()) return Result.HitTerrain;

        _damagePackets.Clear();

        if (CollideWithEntities(player, _damagePackets))
        {
            networkCallback.Invoke(_damagePackets);
            return Result.HitEntity;
        }

        return Result.None;
    }
    public void RenderDebugColliders(IPlayer player, ItemSlot slot, bool rightHand = true)
    {
        LineSegmentCollider.Transform(DamageTypes, player.Entity, slot, _api, rightHand);
        foreach (LineSegmentCollider collider in DamageTypes.Select(item => item.InWorldCollider))
        {
            collider.RenderAsLine(_api, player.Entity);
        }
    }

    private readonly ICoreClientAPI _api;
    private readonly Dictionary<long, float> _currentTime = new();
    private readonly Dictionary<long, float> _totalTime = new();
    private readonly Dictionary<long, HashSet<long>> _attackedEntities = new();
    private readonly List<MeleeAttackDamagePacket> _damagePackets = new();

    private bool CheckTerrainCollision()
    {
        foreach (MeleeAttackDamageType damageType in DamageTypes)
        {
            (Block, System.Numerics.Vector3)? result = damageType._inWorldCollider.IntersectTerrain(_api);

            if (result != null) return true;
        }

        return false;
    }
    private bool CollideWithEntities(IPlayer player, List<MeleeAttackDamagePacket> packets)
    {
        long entityId = player.Entity.EntityId;

        Entity[] entities = _api.World.GetEntitiesAround(player.Entity.Pos.XYZ, MaxReach, MaxReach);

        bool collided = false;
        foreach (MeleeAttackDamageType damageType in DamageTypes)
        {
            foreach (
                Entity entity in entities
                    .Where(entity => _attackedEntities[entityId].Contains(entity.EntityId))
                    .Where(entity => damageType.Collider.RoughIntersect(entity.CollisionBox))
                    .Where(entity => CollideWithEntity(player, damageType, entity, packets))
                )
            {
                collided = true;
            }
        }

        return collided;
    }
    private bool CollideWithEntity(IPlayer player, MeleeAttackDamageType damageType, Entity entity, List<MeleeAttackDamagePacket> packets)
    {
        System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCylinder(new(entity.CollisionBox));

        if (result == null) return false;

        bool attacked = damageType.Attack(player, entity, out MeleeAttackDamagePacket? packet);

        if (packet != null) packets.Add(packet);

        if (attacked)
        {
            _attackedEntities[player.Entity.EntityId].Add(entity.EntityId);
            if (damageType.Sound != null) _api.World.PlaySoundAt(damageType.Sound, entity, randomizePitch: false);
        }

        return attacked;
    }
}

public readonly struct HitWindow
{
    public readonly TimeSpan Start;
    public readonly TimeSpan End;
    public readonly float Progress;
    public HitWindow(TimeSpan start, TimeSpan end)
    {
        Progress = (float)(start / end);
        Start = start;
        End = end;
    }

    public readonly bool Check(float progress) => progress > Progress;
}

public sealed class MeleeAttackDamageType
{
    public LineSegmentCollider Collider { get; }
    public LineSegmentCollider InWorldCollider => _inWorldCollider;
    public AssetLocation? Sound { get; }

    public MeleeAttackDamageType(
        float damage,
        EnumDamageType damageType,
        LineSegmentCollider collider,
        int tier = 0,
        float knockback = 0,
        StatsModifier? damageModifier = null,
        StatsModifier? knockbackModifier = null,
        AssetLocation? sound = null)
    {
        _damage = damage;
        _damageType = damageType;
        _tier = tier;
        _knockback = knockback;
        _damageModifier = damageModifier;
        _knockbackModifier = knockbackModifier;
        Collider = collider;
        _inWorldCollider = collider;
        Sound = sound;
    }

    public bool Attack(IPlayer attacker, Entity target, out MeleeAttackDamagePacket? packet)
    {
        float damage = GetDamage(attacker);
        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = _damageType,
            DamageTier = _tier
        }, damage);

        packet = null;

        if (damageReceived)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) / 10f * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.Add(knockback);

            packet = new()
            {
                Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
                CauseEntity = attacker.Entity.EntityId,
                DamageType = _damageType,
                DamageTier = _tier,
                Damage = damage,
                Knockback = new(knockback.X, knockback.Y, knockback.Z)
            };
        }

        return damageReceived;
    }

    internal LineSegmentCollider _inWorldCollider;

    private readonly float _damage;
    private readonly float _knockback;
    private readonly int _tier;
    private readonly EnumDamageType _damageType;
    private readonly StatsModifier? _damageModifier;
    private readonly StatsModifier? _knockbackModifier;

    private float GetDamage(IPlayer attacker)
    {
        if (_damageModifier == null) return _damage;
        return _damageModifier.Calc(attacker, _damage);
    }
    private float GetKnockback(IPlayer attacker)
    {
        if (_knockbackModifier == null) return _knockback;
        return _knockbackModifier.Calc(attacker, _knockback);
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeAttackDamagePacket
{
    public EnumDamageSource Source { get; set; }
    public long CauseEntity { get; set; }
    public EnumDamageType DamageType { get; set; }
    public int DamageTier { get; set; }
    public float Damage { get; set; }
    public Vector3 Knockback { get; set; }
}
