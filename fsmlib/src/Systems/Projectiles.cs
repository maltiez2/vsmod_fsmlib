using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;



namespace MaltiezFSM.Systems;

public class Projectiles : BaseSystem
{
    private readonly string mReloadSystemName;
    private readonly List<string> mAimingSystemsName = new();
    private readonly string? mDescription;
    private readonly float mProjectileSpeed;
    private readonly float mProjectileDamageMultiplier;
    private readonly int mProjectilesCount;
    private readonly StatsModifier? mDamageModifier;
    private readonly StatsModifier? mSpeedModifier;
    private readonly string mImpactSound;
    private readonly string mHitSound;

    private IItemStackHolder? mReloadSystem;
    private readonly List<IAimingSystem> mAimingSystems = new();

    public Projectiles(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mReloadSystemName = definition["reloadSystem"].AsString("");
        mProjectileSpeed = definition["speed"].AsFloat(1);
        mProjectileDamageMultiplier = definition["damageMultiplier"].AsFloat(1);
        mDescription = definition["description"].AsString();
        mProjectilesCount = definition["projectilesAmount"].AsInt(1);
        if (definition.KeyExists("damage_stats")) mDamageModifier = new(api, definition["damage_stats"].AsString());
        if (definition.KeyExists("speed_stats")) mSpeedModifier = new(api, definition["speed_stats"].AsString());
        mHitSound = definition["hitSound"].AsString("game:sounds/player/projectilehit");
        mImpactSound = definition["impactSound"].AsString("game:sounds/arrow-impact");

        if (definition["aimingSystem"].IsArray())
        {
            foreach (JsonObject? item in definition["aimingSystem"].AsArray())
            {
                mAimingSystemsName.Add(item.AsString());
            }
        }
        else
        {
            mAimingSystemsName.Add(definition["aimingSystem"].AsString());
        }
    }

    public override void SetSystems(Dictionary<string, ISystem> systems)
    {
        if (!systems.ContainsKey(mReloadSystemName))
        {
            IEnumerable<string> reloadSystems = systems.Where((entry, _) => entry.Value is IItemStackHolder).Select((entry, _) => entry.Key);
            Logger.Error(mApi, this, $"System {mCode}. Reload system '{mReloadSystemName}' not found. Available sound systems: {Utils.PrintList(reloadSystems)}.");
            return;
        }

        mReloadSystem = systems[mReloadSystemName] as IItemStackHolder;
        foreach (IAimingSystem? system in mAimingSystemsName.Where(systems.ContainsKey).Where((item) => systems[item] is IAimingSystem).Select((item) => systems[item] as IAimingSystem))
        {
            if (system != null) mAimingSystems.Add(system);
        }

        if (mAimingSystems.Count == 0)
        {
            Logger.Warn(mApi, this, $"System {mCode}. No aiming systems provided or found.");
        }
    }

    public override bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Verify(slot, player, parameters)) return false;

        return mReloadSystem?.Get(slot, player).Count > 0;
    }
    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        List<ItemStack>? ammoStacks = mReloadSystem?.TakeAmount(slot, player, mProjectilesCount);
        if (ammoStacks == null || ammoStacks.Count == 0) return false;

        SpawnProjectiles(ammoStacks, player, slot);

        return true;
    }
    public override string[] GetDescription(ItemSlot slot, IWorldAccessor world)
    {
        if (mDescription == null) return System.Array.Empty<string>();

        return new string[] { Lang.Get(mDescription, mProjectileSpeed, mProjectilesCount, mProjectileDamageMultiplier) };
    }

    private void SpawnProjectiles(List<ItemStack> ammoStacks, IPlayer player, ItemSlot slot)
    {
        Vec3d projectilePosition = ProjectilePosition(player, new Vec3f(0.0f, 0.0f, 0.0f));
        Utils.DirectionOffset dispersion = GetDirectionOffset(slot, player);
        Vec3d projectileVelocity = ProjectileVelocity(player, dispersion);
        float damageMultiplier = GetDamageMultiplier(player);

        foreach (ItemStack ammo in ammoStacks)
        {
            SpawnProjectile(ammo, player, projectilePosition, projectileVelocity, damageMultiplier);
        }
    }
    private Utils.DirectionOffset GetDirectionOffset(ItemSlot slot, IPlayer player)
    {
        Utils.DirectionOffset dispersion = (0f, 0f);
        foreach (IAimingSystem item in mAimingSystems)
        {
            Utils.DirectionOffset offset = item.GetShootingDirectionOffset(slot, player);
            dispersion.Pitch += offset.Pitch;
            dispersion.Yaw += offset.Yaw;
        }
        return dispersion;
    }
    private float GetSpeed(IPlayer player)
    {
        if (mSpeedModifier == null) return mProjectileSpeed;

        return mSpeedModifier.Calc(player, mProjectileSpeed);
    }
    private float GetDamageMultiplier(IPlayer player)
    {
        if (mDamageModifier == null) return mProjectileDamageMultiplier;

        return mDamageModifier.Calc(player, mProjectileDamageMultiplier);
    }
    private static Vec3d ProjectilePosition(IPlayer player, Vec3f muzzlePosition)
    {
        Vec3f worldPosition = Utils.FromCameraReferenceFrame(player.Entity, muzzlePosition);
        return player.Entity.SidedPos.AheadCopy(0).XYZ.Add(worldPosition.X, player.Entity.LocalEyePos.Y + worldPosition.Y, worldPosition.Z);
    }
    private Vec3d ProjectileVelocity(IPlayer player, Utils.DirectionOffset dispersion)
    {
        Vec3d pos = player.Entity.ServerPos.XYZ.Add(0, player.Entity.LocalEyePos.Y, 0);
        Vec3d aheadPos = pos.AheadCopy(1, player.Entity.SidedPos.Pitch + dispersion.Pitch, player.Entity.SidedPos.Yaw + dispersion.Yaw);
        return (aheadPos - pos).Normalize() * GetSpeed(player);
    }
    private void SpawnProjectile(ItemStack projectileStack, IPlayer player, Vec3d position, Vec3d velocity, float damageMultiplier)
    {
        if (projectileStack?.Item?.Code == null) return;

        List<ProjectileDamageType>? damageTypes = projectileStack.Collectible.GetBehavior<AdvancedProjectileBehavior>()?.DamageTypes;

        if (damageTypes == null || damageTypes.Count == 0)
        {
            Logger.Warn(mApi, this, $"System '{mCode}'. Failed to retrieve damage types from '{projectileStack.Item.Code}'. Try adding FSMAdvancedProjectile behavior to projectile itemtype/blocktype.");
            return;
        }

        AssetLocation entityTypeAsset = new(projectileStack.Collectible.Attributes["projectile"].AsString(projectileStack.Collectible.Code.Path));

        EntityProperties entityType = mApi.World.GetEntityType(entityTypeAsset);

        if (entityType == null)
        {
            Logger.Warn(mApi, this, $"System '{mCode}'. Failed to create entity '{entityTypeAsset}'.");
            return;
        }


        if (mApi.ClassRegistry.CreateEntity(entityType) is not AdvancedEntityProjectile projectile)
        {
            Logger.Warn(mApi, this, $"System '{mCode}'. Entity '{entityTypeAsset}' should have 'AdvancedEntityProjectile' class or its subclass.");
            return;
        }

        projectile.FiredBy = player.Entity;
        projectile.ProjectileStack = projectileStack;
        projectile.DropOnImpactChance = 1 - projectileStack.Collectible.Attributes["breakChanceOnImpact"].AsFloat(0);
        projectile.ServerPos.SetPos(position);
        projectile.ServerPos.Motion.Set(velocity);
        projectile.Pos.SetFrom(projectile.ServerPos);
        projectile.World = mApi.World;
        projectile.SetRotation();

        projectile.DamageTypes = damageTypes;
        projectile.DamageMultiplier = damageMultiplier;
        projectile.HitSound = mHitSound;
        projectile.ImpactSound = mImpactSound;

        mApi.World.SpawnEntity(projectile);
    }
}
