using ProtoBuf;
using System.Collections.Immutable;
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
        _channel = api.Network.RegisterChannel(networkId)
            .RegisterMessageType<MeleeAttackPacket>();
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
        if (_timers.ContainsKey(id))
        {
            _api.World.UnregisterGameTickListener(_timers[id]);
            _timers.Remove(id);
        }
    }


    private long _nextId = 0;
    private readonly ICoreClientAPI _api;
    private readonly Dictionary<long, long> _timers = new();
    private readonly IClientNetworkChannel _channel;

    private void Step(float dt, MeleeAttack attack, IPlayer player, ItemSlot slot, bool rightHand, System.Func<MeleeAttack.AttackResult, bool> callback, long id)
    {
        MeleeAttack.AttackResult result = attack.Step(player, dt, slot, Synchronize, rightHand);

        if (result.Result == MeleeAttack.Result.None) return;

        if (callback.Invoke(result) || result.Result == MeleeAttack.Result.Finished)
        {
            Stop(id);
        }
    }
    private void Synchronize(List<MeleeAttackDamagePacket> packets)
    {
        MeleeAttackPacket packet = new()
        {
            Damages = packets.ToArray()
        };

        _channel.SendPacket(packet);
    }
}
public sealed class MeleeServer : BaseSystem
{
    public MeleeServer(ICoreServerAPI api, string networkId, string debugName = "") : base(api, debugName)
    {
        _api = api;
        api.Network.RegisterChannel(networkId)
            .RegisterMessageType<MeleeAttackPacket>()
            .SetMessageHandler<MeleeAttackPacket>(HandlePacket);
    }

    private readonly ICoreServerAPI _api;

    private void HandlePacket(IServerPlayer player, MeleeAttackPacket packet)
    {
        foreach (MeleeAttackDamagePacket damagePacket in packet.Damages)
        {
            ApplyDamage(damagePacket);
        }
    }
    private void ApplyDamage(MeleeAttackDamagePacket packet)
    {
        Entity target = _api.World.GetEntityById(packet.Target);
        Entity attacker = _api.World.GetEntityById(packet.CauseEntity);

        target.ReceiveDamage(new DamageSource()
        {
            Source = packet.Source,
            SourceEntity = null,
            CauseEntity = attacker,
            Type = packet.DamageType,
            DamageTier = packet.DamageTier
        }, packet.Damage);
    }
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

    public readonly struct AttackResult
    {
        public readonly Result Result;
        public readonly IEnumerable<(Block block, Vector3 point)>? Terrain;
        public readonly IEnumerable<(Entity entity, Vector3 point)>? Entities;

        public AttackResult(Result result = Result.None, IEnumerable<(Block block, Vector3 point)>? terrain = null, IEnumerable<(Entity entity, Vector3 point)>? entities = null)
        {
            Result = result;
            Terrain = terrain;
            Entities = entities;
        }
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
    public AttackResult Step(IPlayer player, float dt, ItemSlot slot, Action<List<MeleeAttackDamagePacket>> networkCallback, bool rightHand = true)
    {
        _currentTime[player.Entity.EntityId] += dt * 1000;
        float progress = GameMath.Clamp(_currentTime[player.Entity.EntityId] / _totalTime[player.Entity.EntityId], 0, 1);
        Console.WriteLine(progress);
        if (progress >= 1) return new(Result.Finished);
        if (!Window.Check(progress)) return new(Result.None);

        bool success = LineSegmentCollider.Transform(DamageTypes, player.Entity, slot, _api, rightHand);
        if (!success) return new(Result.None);

        IEnumerable<(Block block, Vector3 point)> terrainCollisions = CheckTerrainCollision();

        if (terrainCollisions.Any()) return new(Result.HitTerrain, terrain: terrainCollisions);

        _damagePackets.Clear();

        IEnumerable<(Entity entity, Vector3 point)> entitiesCollisions = CollideWithEntities(player, _damagePackets);

        if (entitiesCollisions.Any())
        {
            networkCallback.Invoke(_damagePackets);
            return new(Result.HitEntity, entities: entitiesCollisions);
        }

        return new(Result.None);
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
    private readonly HashSet<(Block block, Vector3 point)> _terrainCollisionsBuffer = new();
    private readonly HashSet<(Entity entity, Vector3 point)> _entitiesCollisionsBuffer = new();

    private IEnumerable<(Block block, Vector3 point)> CheckTerrainCollision()
    {
        _terrainCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes)
        {
            (Block, System.Numerics.Vector3)? result = damageType._inWorldCollider.IntersectTerrain(_api);

            if (result != null)
            {
                _terrainCollisionsBuffer.Add(result.Value);
            }
        }

        return _terrainCollisionsBuffer.ToImmutableHashSet();
    }
    private IEnumerable<(Entity entity, Vector3 point)> CollideWithEntities(IPlayer player, List<MeleeAttackDamagePacket> packets)
    {
        long entityId = player.Entity.EntityId;

        Entity[] entities = _api.World.GetEntitiesAround(player.Entity.Pos.XYZ, MaxReach, MaxReach);

        _entitiesCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes)
        {
            foreach (
                (Entity entity, Vector3 point) entry in entities
                    .Where(entity => entity != player.Entity)
                    .Where(entity => !_attackedEntities[entityId].Contains(entity.EntityId))
                    .Where(entity => damageType.InWorldCollider.RoughIntersect(GetCollisionBox(entity)))
                    .Select(entity => (entity, CollideWithEntity(player, damageType, entity, packets)))
                    .Where(entry => entry.Item2 != null)
                    .Select(entry => (entry.entity, entry.Item2.Value))
                )
            {
                _entitiesCollisionsBuffer.Add(entry);
            }
        }

        return _entitiesCollisionsBuffer.ToImmutableHashSet();
    }
    private Cuboidf GetCollisionBox(Entity entity)
    {
        Cuboidf collisionBox = entity.CollisionBox.Clone();
        EntityPos position = entity.Pos;
        collisionBox.X1 += (float)position.X;
        collisionBox.Y1 += (float)position.Y;
        collisionBox.Z1 += (float)position.Z;
        collisionBox.X2 += (float)position.X;
        collisionBox.Y2 += (float)position.Y;
        collisionBox.Z2 += (float)position.Z;
        return collisionBox;
    }
    private System.Numerics.Vector3? CollideWithEntity(IPlayer player, MeleeAttackDamageType damageType, Entity entity, List<MeleeAttackDamagePacket> packets)
    {
        //System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCylinder(new(GetCollisionBox(entity)));
        System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCuboid(GetCollisionBox(entity));

        if (result == null) return null;

        AdvancedParticleProperties advancedParticleProperties = new();
        advancedParticleProperties.basePos.X = result.Value.X;
        advancedParticleProperties.basePos.Y = result.Value.Y;
        advancedParticleProperties.basePos.Z = result.Value.Z;
        entity.World.SpawnParticles(advancedParticleProperties);

        bool attacked = damageType.Attack(player, entity, out MeleeAttackDamagePacket? packet);

        if (packet != null) packets.Add(packet);

        if (attacked)
        {
            _attackedEntities[player.Entity.EntityId].Add(entity.EntityId);
            if (damageType.Sound != null) _api.World.PlaySoundAt(damageType.Sound, entity, randomizePitch: false);
        }

        return attacked ? result : null;
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
                Target = target.EntityId,
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
public sealed class MeleeAttackPacket
{
    public MeleeAttackDamagePacket[] Damages { get; set; } = Array.Empty<MeleeAttackDamagePacket>();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeAttackDamagePacket
{
    public long Target { get; set; }
    public EnumDamageSource Source { get; set; }
    public long CauseEntity { get; set; }
    public EnumDamageType DamageType { get; set; }
    public int DamageTier { get; set; }
    public float Damage { get; set; }
    public Vector3 Knockback { get; set; }
}
