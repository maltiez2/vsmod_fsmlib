using CompactExifLib;
using MaltiezFSM.Systems;
using OpenTK.Windowing.GraphicsLibraryFramework;
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

    public TimeSpan Duration { get; }
    public IEnumerable<MeleeAttackDamageType> DamageTypes { get; }
    public StatsModifier? DurationModifier { get; }
    public float MaxReach { get; }

    public MeleeAttack(ICoreClientAPI api, TimeSpan duration, IEnumerable<MeleeAttackDamageType> damageTypes, float maxReach, StatsModifier? durationModifier = null)
    {
        _api = api;
        Duration = duration;
        DamageTypes = damageTypes;
        DurationModifier = durationModifier;
        MaxReach = maxReach;
    }

    public void Start(IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        _currentTime[entityId] = 0;
        _totalTime[entityId] = DurationModifier?.Calc(player, (float)Duration.TotalMilliseconds) ?? (float)Duration.TotalMilliseconds;
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
        if (progress >= 1)
        {
            return new(Result.Finished);
        }

        bool success = LineSegmentCollider.Transform(DamageTypes, player.Entity, slot, _api, rightHand);
        if (!success) return new(Result.None);

        IEnumerable<(Block block, Vector3 point)> terrainCollisions = CheckTerrainCollision(progress);

        if (terrainCollisions.Any()) return new(Result.HitTerrain, terrain: terrainCollisions);

        _damagePackets.Clear();

        IEnumerable<(Entity entity, Vector3 point)> entitiesCollisions = CollideWithEntities(progress, player, _damagePackets);

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

    private IEnumerable<(Block block, Vector3 point)> CheckTerrainCollision(float progress)
    {
        _terrainCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes.Where(item => item.Window.Check(progress)))
        {
            (Block block, Vector3 position)? result = damageType._inWorldCollider.IntersectTerrain(_api);

            if (result != null)
            {
                _terrainCollisionsBuffer.Add(result.Value);
                Vector3 direction = damageType._inWorldCollider.Direction / damageType._inWorldCollider.Direction.Length() * -1;
                damageType.Effects.OnTerrainCollision(result.Value.block, result.Value.position, direction, _api);
            }
        }

        return _terrainCollisionsBuffer.ToImmutableHashSet();
    }
    private IEnumerable<(Entity entity, Vector3 point)> CollideWithEntities(float progress, IPlayer player, List<MeleeAttackDamagePacket> packets)
    {
        long entityId = player.Entity.EntityId;

        Entity[] entities = _api.World.GetEntitiesAround(player.Entity.Pos.XYZ, MaxReach, MaxReach);

        _entitiesCollisionsBuffer.Clear();
        foreach (MeleeAttackDamageType damageType in DamageTypes.Where(item => item.Window.Check(progress)))
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
            Vector3 direction = damageType._inWorldCollider.Direction / damageType._inWorldCollider.Direction.Length() * -1;
            damageType.Effects.OnEntityCollision(entity, result.Value, direction, _api);
        }

        return attacked ? result : null;
    }
}

public readonly struct HitWindow
{
    public readonly float Start;
    public readonly float End;
    public HitWindow(float start, float end)
    {
        Start = start;
        End = end;
    }

    public readonly bool Check(float progress) => progress >= Start && progress <= End;
}

public sealed class CollisionEffects
{
    public Dictionary<string, AssetLocation> EntityCollisionSounds { get; set; } = new();
    public Dictionary<string, AssetLocation> TerrainCollisionSounds { get; set; } = new();
    public Dictionary<string, (AdvancedParticleProperties effect, float directionFactor)> TerrainCollisionParticles { get; set; } = new();
    public Dictionary<string, (AdvancedParticleProperties effect, float directionFactor)> EntityCollisionParticles { get; set; } = new();

    public void OnTerrainCollision(Block block, Vector3 position, Vector3 direction, ICoreAPI api)
    {
        foreach (AssetLocation sound in TerrainCollisionSounds.Where(entry => WildcardUtil.Match(entry.Key, block.Code.ToString())).Select(entry => entry.Value))
        {
            api.World.PlaySoundAt(sound, position.X, position.Y, position.Z, randomizePitch: true);
            break;
        }

        foreach ((AdvancedParticleProperties effect, float directionFactor) in TerrainCollisionParticles.Where(entry => WildcardUtil.Match(entry.Key, block.Code.ToString())).Select(entry => entry.Value))
        {
            effect.basePos.X = position.X;
            effect.basePos.Y = position.Y;
            effect.basePos.Z = position.Z;
            effect.baseVelocity.X = direction.X * directionFactor;
            effect.baseVelocity.Y = direction.Y * directionFactor;
            effect.baseVelocity.Z = direction.Z * directionFactor;
            api.World.SpawnParticles(effect);
        }
    }
    public void OnEntityCollision(Entity entity, Vector3 position, Vector3 direction, ICoreAPI api)
    {
        foreach (AssetLocation sound in EntityCollisionSounds.Where(entry => WildcardUtil.Match(entry.Key, entity.Code.ToString())).Select(entry => entry.Value))
        {
            api.World.PlaySoundAt(sound, entity, randomizePitch: true);
            break;
        }

        foreach ((AdvancedParticleProperties effect, float directionFactor) in EntityCollisionParticles.Where(entry => WildcardUtil.Match(entry.Key, entity.Code.ToString())).Select(entry => entry.Value))
        {
            effect.basePos.X = position.X;
            effect.basePos.Y = position.Y;
            effect.basePos.Z = position.Z;
            effect.baseVelocity.X = direction.X * directionFactor;
            effect.baseVelocity.Y = direction.Y * directionFactor;
            effect.baseVelocity.Z = direction.Z * directionFactor;
            api.World.SpawnParticles(effect);
        }
    }
}

public sealed class MeleeAttackDamageType
{
    public LineSegmentCollider Collider { get; }
    public LineSegmentCollider InWorldCollider => _inWorldCollider;
    public HitWindow Window { get; }
    public CollisionEffects Effects { get; set; }

    public MeleeAttackDamageType(
        float damage,
        EnumDamageType damageType,
        LineSegmentCollider collider,
        HitWindow hitWindow,
        int tier = 0,
        float knockback = 0,
        float stagger = 1.0f,
        StatsModifier? damageModifier = null,
        StatsModifier? knockbackModifier = null,
        CollisionEffects? effects = null)
    {
        _damage = damage;
        _damageType = damageType;
        _tier = tier;
        _knockback = knockback;
        _stagger = stagger;
        _damageModifier = damageModifier;
        _knockbackModifier = knockbackModifier;
        Collider = collider;
        Window = hitWindow;
        _inWorldCollider = collider;
        Effects = effects ?? new();
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

        bool received = damageReceived || damage <= 0;

        if (received)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) * _knockbackFactor * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.X *= _stagger;
            target.SidedPos.Motion.Z *= _stagger;
            target.SidedPos.Motion.Add(knockback);

            packet = new()
            {
                Target = target.EntityId,
                Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
                CauseEntity = attacker.Entity.EntityId,
                DamageType = _damageType,
                DamageTier = _tier,
                Damage = damage,
                Knockback = new float[] { knockback.X, knockback.Y, knockback.Z },
                Stagger = _stagger
            };
        }

        return received;
    }

    internal LineSegmentCollider _inWorldCollider;

    private readonly float _damage;
    private readonly float _knockback;
    private readonly float _stagger;
    private readonly int _tier;
    private readonly EnumDamageType _damageType;
    private readonly StatsModifier? _damageModifier;
    private readonly StatsModifier? _knockbackModifier;
    private const float _knockbackFactor = 0.0625f;

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