using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
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

    private JsonObject? mProperties;

    public AdvancedProjectileBehavior(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (mProperties == null) return;

        foreach (JsonObject damageType in mProperties["damageTypes"].AsArray())
        {
            DamageTypes.Add(new(damageType, api));
        }
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        mProperties = properties;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

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

    private const float cSpeedThreshold = 0.1f;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);

        float speed = (float)ServerPos.Motion.Length();

        if (speed > cSpeedThreshold) SpeedBeforeCollision = speed;
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
    private readonly float mDamage;
    private readonly float mKnockback;
    private readonly string mSound;
    private readonly int mTier;
    private readonly EnumDamageType mDamageType;
    private readonly StatsModifier? mDamageModifier;
    private readonly StatsModifier? mKnockbackModifier;
    private readonly ValueModifier? mDamageSpeedModifier;

    private ISoundSystem? mSoundSystem;

    public ProjectileDamageType(JsonObject definition, ICoreAPI api)
    {
        mTier = definition["tier"].AsInt(0);
        mSound = definition["sound"].AsString();
        mDamage = definition["damage"].AsFloat();
        mKnockback = definition["knockback"].AsFloat(0);

        mDamageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), definition["type"].AsString("PiercingAttack"));

        if (definition.KeyExists("damage_stats")) mDamageModifier = new(api, definition["damage_stats"].AsString());
        if (definition.KeyExists("knockback_stats")) mKnockbackModifier = new(api, definition["knockback_stats"].AsString());
        if (definition.KeyExists("speed_affect")) mDamageSpeedModifier = new(api, definition["speed_affect"].AsString());
    }

    public void SetSoundSystem(ISoundSystem system) => mSoundSystem = system;

    public bool Attack(IPlayer attacker, Entity target, float speed, float damageMultiplier = 1)
    {
        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = mDamageType,
            DamageTier = mTier
        }, GetDamage(attacker, speed) * damageMultiplier);

        if (damageReceived)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) / 10f * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.Add(knockback);

            if (mSound != null) mSoundSystem?.PlaySound(mSound, target);
        }

        return damageReceived;
    }

    private float GetDamage(IPlayer attacker, float speed)
    {
        float damage = mDamage;

        if (mDamageModifier != null) damage = mDamageModifier.Calc(attacker, damage);
        if (mDamageSpeedModifier != null) damage = mDamageSpeedModifier.Calc(mDamage, name => name == "speed" ? speed : 0);

        return damage;
    }
    private float GetKnockback(IPlayer attacker)
    {
        if (mKnockbackModifier == null) return mKnockback;
        return mKnockbackModifier.Calc(attacker, mKnockback);
    }

    public void GetHeldItemInfo(StringBuilder dsc, IWorldAccessor world)
    {
        dsc.AppendLine($"  {GetDamageTypeName()}");
        dsc.AppendLine($"    {GetDamageEntry(world)}");
        dsc.AppendLine($"    {GetTierEntry()}");
        if (mKnockback > 0) dsc.AppendLine($"    {GetKnockbackEntry(world)}");
    }

    private string GetDamageTypeName()
    {
        return Lang.Get($"fsmlib:damage-type-{mDamageType.ToString().ToLower()}");
    }
    private string GetKnockbackEntry(IWorldAccessor world)
    {
        if (world is not IClientWorldAccessor clientWorld) return "";
        float value = GetKnockback(clientWorld.Player);
        return Lang.Get("fsmlib:damage-knockback", value);
    }
    private string GetTierEntry()
    {
        return Lang.Get("fsmlib:damage-tier", mTier);
    }
    private string GetDamageEntry(IWorldAccessor world)
    {
        if (world is not IClientWorldAccessor clientWorld) return "";
        float damage = mDamage;
        if (mDamageModifier != null) damage = mDamageModifier.Calc(clientWorld.Player, damage);
        return Lang.Get("fsmlib:damage-damage", damage);
    }
}
