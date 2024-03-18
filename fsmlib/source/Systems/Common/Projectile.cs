using MaltiezFSM.Framework;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace MaltiezFSM.Systems;

public class AdvancedProjectileBehavior : CollectibleBehavior
{
    public List<ProjectileDamageType> DamageTypes { get; set; } = new();
    public int AdditionalDurabilityCost { get; set; } = 0;
    public AssetLocation ImpactSound { get; set; } = new("game:sounds/arrow-impact");
    public AssetLocation HitSound { get; set; } = new("game:sounds/player/projectilehit");

    private JsonObject? _properties;

    public AdvancedProjectileBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (_properties == null) return;

        foreach (JsonObject damageType in _properties["damageTypes"].AsArray())
        {
            DamageTypes.Add(new(damageType, api));
        }
        AdditionalDurabilityCost = _properties["additionalDurabilityCost"].AsInt(0);
        if (_properties.KeyExists("impactSound")) ImpactSound = new(_properties["impactSound"].AsString());
        if (_properties.KeyExists("hitSound")) HitSound = new(_properties["hitSound"].AsString());
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        _properties = properties;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        if (AdditionalDurabilityCost != 0)
        {
            dsc.AppendLine($"{Lang.Get("fsmlib:projectile-durability-cost", AdditionalDurabilityCost)}");
        }

        dsc.AppendLine($"{Lang.Get("fsmlib:damage-list")}");
        foreach (ProjectileDamageType item in DamageTypes)
        {
            item.GetHeldItemInfo(dsc, world);
        }
        dsc.AppendLine();
    }
}

public class AdvancedEntityProjectile : EntityProjectile
{
    public List<ProjectileDamageType> DamageTypes { get; set; } = new();
    public string? ImpactSound { get; set; }
    public string? HitSound { get; set; }
    public float? DamageMultiplier { get; set; }
    public float SpeedBeforeCollision { get; set; } = 0;

    private const float _speedThreshold = 0.1f;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);

        float speed = (float)ServerPos.Motion.Length();

        if (speed > _speedThreshold) SpeedBeforeCollision = speed;
    }

    public static bool ImpactOnEntityPatch(EntityProjectile __instance, Entity entity)
    {
        if (__instance is not AdvancedEntityProjectile advancedProjectile) return true;
        advancedProjectile.ImpactOnEntity(entity);
        return false;
    }
    public void ImpactOnEntity(Entity entity)
    {
        if (!CanDamage(entity) || World.Side != EnumAppSide.Server) return;

        _ = new Utils.Field<long, AdvancedEntityProjectile>(typeof(EntityProjectile), "msCollide", this)
        {
            Value = World.ElapsedMilliseconds
        };

        if (ImpactSound != null) World.PlaySoundAt(new AssetLocation(ImpactSound), this, null, false, 24);

        bool didDamage = TryDamage(entity);

        if (!DamageProjectile(entity) || World.Rand.NextDouble() > DropOnImpactChance)
        {
            Die();
        }

        if (FiredBy is EntityPlayer && didDamage && HitSound != null)
        {
            World.PlaySoundFor(new AssetLocation(HitSound), (FiredBy as EntityPlayer)?.Player, false, 24);
        }
    }

    protected virtual bool CanDamage(Entity target)
    {
        if (!Alive) return false;

        bool targetIsPlayer = target is EntityPlayer;
        bool targetIsCreature = target is EntityAgent;

        if (FiredBy is EntityPlayer { Player: IServerPlayer fromPlayer } && World.Api is ICoreServerAPI serverApi)
        {
            if (targetIsPlayer && (!serverApi.Server.Config.AllowPvP || !fromPlayer.HasPrivilege("attackplayers"))) return false;
            if (targetIsCreature && !fromPlayer.HasPrivilege("attackcreatures")) return false;
        }

        return true;
    }
    protected virtual bool TryDamage(Entity target)
    {
        IPlayer? attacker = (FiredBy as EntityPlayer)?.Player;
        if (attacker == null) return false;

        bool damageReceived = false;

        foreach (ProjectileDamageType damageType in DamageTypes)
        {
            if (damageType.Attack(attacker, target, SpeedBeforeCollision, DamageMultiplier ?? 1)) damageReceived = true;
        }

        return damageReceived;
    }
    protected virtual bool DamageProjectile(Entity entity)
    {
        int leftDurability = 1;
        if (DamageStackOnImpact)
        {
            ProjectileStack?.Collectible.DamageItem(entity.World, entity, new DummySlot(ProjectileStack));
            leftDurability = ProjectileStack == null ? 1 : ProjectileStack.Collectible.GetRemainingDurability(ProjectileStack);
        }
        return leftDurability > 0;
    }
}

public sealed class ProjectileDamageType
{
    private readonly float _damage;
    private readonly float _knockback;
    private readonly string _sound;
    private readonly int _tier;
    private readonly EnumDamageType _damageType;
    private readonly StatsModifier? _damageModifier;
    private readonly StatsModifier? _knockbackModifier;
    private readonly ValueModifier? _damageSpeedModifier;

    private ISoundSystem? mSoundSystem;

    public ProjectileDamageType(JsonObject definition, ICoreAPI api)
    {
        _tier = definition["tier"].AsInt(0);
        _sound = definition["sound"].AsString();
        _damage = definition["damage"].AsFloat();
        _knockback = definition["knockback"].AsFloat(0);

        _damageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), definition["type"].AsString("PiercingAttack"));

        if (definition.KeyExists("damage_stats")) _damageModifier = new(api, definition["damage_stats"].AsString());
        if (definition.KeyExists("knockback_stats")) _knockbackModifier = new(api, definition["knockback_stats"].AsString());
        if (definition.KeyExists("speed_affect")) _damageSpeedModifier = new(api, definition["speed_affect"].AsString());
    }

    public void SetSoundSystem(ISoundSystem system) => mSoundSystem = system;

    public bool Attack(IPlayer attacker, Entity target, float speed, float damageMultiplier = 1)
    {
        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = _damageType,
            DamageTier = _tier
        }, GetDamage(attacker, speed) * damageMultiplier);

        if (damageReceived)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) / 10f * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.Add(knockback);

            if (_sound != null) mSoundSystem?.PlaySound(_sound, target);
        }

        return damageReceived;
    }

    private float GetDamage(IPlayer attacker, float speed)
    {
        float damage = _damage;

        if (_damageModifier != null) damage = _damageModifier.Calc(attacker, damage);
        if (_damageSpeedModifier != null) damage = _damageSpeedModifier.Calc(_damage, name => name == "speed" ? speed : 0);

        return damage;
    }
    private float GetKnockback(IPlayer attacker)
    {
        if (_knockbackModifier == null) return _knockback;
        return _knockbackModifier.Calc(attacker, _knockback);
    }

    public void GetHeldItemInfo(StringBuilder dsc, IWorldAccessor world)
    {
        dsc.AppendLine($"  {GetDamageTypeName()}");
        dsc.AppendLine($"    {GetDamageEntry(world)}");
        dsc.AppendLine($"    {GetTierEntry()}");
        if (_knockback > 0) dsc.AppendLine($"    {GetKnockbackEntry(world)}");
    }

    private string GetDamageTypeName()
    {
        return Lang.Get($"fsmlib:damage-type-{_damageType.ToString().ToLower()}");
    }
    private string GetKnockbackEntry(IWorldAccessor world)
    {
        if (world is not IClientWorldAccessor clientWorld) return "";
        float value = GetKnockback(clientWorld.Player);
        return Lang.Get("fsmlib:damage-knockback", value);
    }
    private string GetTierEntry()
    {
        return Lang.Get("fsmlib:damage-tier", _tier);
    }
    private string GetDamageEntry(IWorldAccessor world)
    {
        if (world is not IClientWorldAccessor clientWorld) return "";
        float damage = _damage;
        if (_damageModifier != null) damage = _damageModifier.Calc(clientWorld.Player, damage);
        return Lang.Get("fsmlib:damage-damage", damage);
    }
}
