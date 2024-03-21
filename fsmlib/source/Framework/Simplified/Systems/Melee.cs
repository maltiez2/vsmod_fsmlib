using System.Collections.Immutable;
using System.Drawing;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace MaltiezFSM.Framework.Simplified.Systems;

public sealed class MeleeSystem : BaseSystem
{
    public MeleeSystem(ICoreAPI api, string debugName = "") : base(api, debugName)
    {
        _clientApi = api as ICoreClientAPI;
        _serverApi = api as ICoreServerAPI;
    }

    public void StartClientSide(long id, IPlayer player, MeleeAttack attack, ItemSlot slot, System.Func<MeleeAttack.AttackResult, bool> callback, bool rightHand = true)
    {
        if (_clientApi == null) throw new InvalidOperationException();

        Stop(id, player);
        attack.Start(player);
        long timer = _clientApi.World.RegisterGameTickListener(dt => Step(dt, attack, player, slot, rightHand, callback, id), 0);
        _timers[id] = timer;
    }
    public void StartServerSide(long id, IPlayer player, MeleeAttack attack, System.Func<MeleeCollisionPacket, bool> callback)
    {
        if (_serverApi == null) throw new InvalidOperationException();

        MeleeSynchronizer._attacks[(player.Entity.EntityId, id)] = (packet =>
        {
            if (callback.Invoke(packet) || packet.Finished)
            {
                Stop(id, player);
            }
        }, attack.MaxReach);
    }
    public void Stop(long id, IPlayer player)
    {
        if (_clientApi != null && _timers.ContainsKey(id))
        {
            _clientApi.World.UnregisterGameTickListener(_timers[id]);
            _timers.Remove(id);
        }

        if (_serverApi != null)
        {
            (long EntityId, long id) fullId = (player.Entity.EntityId, id);
            MeleeSynchronizer._attacks.Remove(fullId);
        }
    }


    private readonly ICoreClientAPI? _clientApi;
    private readonly ICoreServerAPI? _serverApi;
    private readonly Dictionary<long, long> _timers = new();

    private void Step(float dt, MeleeAttack attack, IPlayer player, ItemSlot slot, bool rightHand, System.Func<MeleeAttack.AttackResult, bool> callback, long id)
    {
        MeleeAttack.AttackResult result = attack.Step(player, dt, slot, packet => SynchronizeDamage(packet, player, id), rightHand);

        if (result.Result == MeleeAttack.Result.None) return;

        SynchronizeCollisions(result, id);

        if (callback.Invoke(result) || result.Result == MeleeAttack.Result.Finished)
        {
            Stop(id, player);
        }
    }
    private static void SynchronizeDamage(List<MeleeAttackDamagePacket> packets, IPlayer player, long id)
    {
        MeleeAttackPacket packet = new()
        {
            AttackId = id,
            PlayerId = player.Entity.EntityId,
            Damages = packets.ToArray()
        };

        MeleeSynchronizer.Send(packet);
    }
    private static void SynchronizeCollisions(MeleeAttack.AttackResult result, long id)
    {
        MeleeCollisionPacket packet = new()
        {
            Id = id,
            Finished = result.Result == MeleeAttack.Result.Finished,
            Blocks = result.Terrain?.Select(entry => entry.block.Code.ToString()).ToArray() ?? Array.Empty<string>(),
            Entities = result.Entities?.Select(entry => entry.entity.EntityId).ToArray() ?? Array.Empty<long>()
        };
        MeleeSynchronizer.Send(packet);
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
        if (progress >= 1)
        {
            return new(Result.Finished);
        }
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
            (Block block, Vector3 position)? result = damageType._inWorldCollider.IntersectTerrain(_api);

            if (result != null)
            {
                _terrainCollisionsBuffer.Add(result.Value);
                SpawnTerrainCollisionParticles(damageType, result.Value.block, result.Value.position);
                if (damageType.TerrainCollisionSound != null) _api.World.PlaySoundAt(damageType.TerrainCollisionSound, result.Value.position.X, result.Value.position.Y, result.Value.position.Z, randomizePitch: false);
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
    private static Cuboidf GetCollisionBox(Entity entity)
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
    private Vector3? CollideWithEntity(IPlayer player, MeleeAttackDamageType damageType, Entity entity, List<MeleeAttackDamagePacket> packets)
    {
        //System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCylinder(new(GetCollisionBox(entity)));
        System.Numerics.Vector3? result = damageType._inWorldCollider.IntersectCuboid(GetCollisionBox(entity));

        if (result == null) return null;

        bool attacked = damageType.Attack(player, entity, out MeleeAttackDamagePacket? packet);

        if (packet != null) packets.Add(packet);

        if (attacked)
        {
            _attackedEntities[player.Entity.EntityId].Add(entity.EntityId);
            SpawnEntityCollisionParticles(damageType, entity, result.Value);
            if (damageType.EntityCollisionSound != null) _api.World.PlaySoundAt(damageType.EntityCollisionSound, entity, randomizePitch: false);
        }

        return attacked ? result : null;
    }
    private void SpawnTerrainCollisionParticles(MeleeAttackDamageType damageType, Block block, Vector3 position)
    {
        foreach (AdvancedParticleProperties particles in damageType.TerrainCollisionParticles.Where(entry => WildcardUtil.Match(entry.Key, block.Code.ToString())).Select(entry => entry.Value))
        {
            particles.basePos.X = position.X;
            particles.basePos.Y = position.Y;
            particles.basePos.Z = position.Z;
            _api.World.SpawnParticles(particles);
        }
    }
    private void SpawnEntityCollisionParticles(MeleeAttackDamageType damageType, Entity entity, Vector3 position)
    {
        foreach (AdvancedParticleProperties particles in damageType.EntityCollisionParticles.Where(entry => WildcardUtil.Match(entry.Key, entity.Code.ToString())).Select(entry => entry.Value))
        {
            particles.basePos.X = position.X;
            particles.basePos.Y = position.Y;
            particles.basePos.Z = position.Z;
            _api.World.SpawnParticles(particles);
        }
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
    public AssetLocation? EntityCollisionSound { get; }
    public AssetLocation? TerrainCollisionSound { get; }
    public Dictionary<string, AdvancedParticleProperties> TerrainCollisionParticles { get; set; } = new();
    public Dictionary<string, AdvancedParticleProperties> EntityCollisionParticles { get; set; } = new();

    public MeleeAttackDamageType(
        float damage,
        EnumDamageType damageType,
        LineSegmentCollider collider,
        int tier = 0,
        float knockback = 0,
        StatsModifier? damageModifier = null,
        StatsModifier? knockbackModifier = null,
        AssetLocation? hitSound = null,
        AssetLocation? terrainSound = null)
    {
        _damage = damage;
        _damageType = damageType;
        _tier = tier;
        _knockback = knockback;
        _damageModifier = damageModifier;
        _knockbackModifier = knockbackModifier;
        Collider = collider;
        _inWorldCollider = collider;
        TerrainCollisionSound = terrainSound;
        EntityCollisionSound = hitSound;
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
                Knockback = new float[] { knockback.X, knockback.Y, knockback.Z }
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