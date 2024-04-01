using MaltiezFSM.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Framework.Simplified.Systems;

public class Projectiles : BaseSystem
{
    public Projectiles(ICoreAPI api, string debugName = "") : base(api, debugName)
    {
    }

    public delegate DirectionOffset DispersionDelegate(IPlayer player);

    public StatsModifier? DamageModifier { get; set; }
    public StatsModifier? SpeedModifier { get; set; }
    public DispersionDelegate? OffsetGetter { get; set; }

    /// <summary>
    /// Spawns projectile for each item in provided <see cref="ItemStack"/>s, each projectile will have its on direction offset due to dispersion.
    /// </summary>
    /// <param name="projectiles">Stacks that are used to get projectile stats. One projectile is spawned for each item in stack.</param>
    /// <param name="player">Shooter.</param>
    /// <param name="speed">Projectile speed in blocks/second</param>
    /// <param name="damageMultiplier">Multiplies projectile damage</param>
    /// <param name="offset">Additional offset to projectile direction that is same for all projectiles from provided items collection.</param>
    /// <returns>Total additional durability damage</returns>
    public int Spawn(List<ItemStack> projectiles, IPlayer player, float speed = 1.0f, float damageMultiplier = 1.0f, DirectionOffset? offset = null)
    {
        try
        {
            return SpawnProjectiles(projectiles, player, speed, damageMultiplier, offset ?? DirectionOffset.Zero);
        }
        catch (Exception exception)
        {
            LogError($"Exception on spawning projectile:\n{exception}");
            return 0;
        }
    }

    private int SpawnProjectiles(List<ItemStack> ammoStacks, IPlayer player, float speed, float damageMultiplier, DirectionOffset offset)
    {
        damageMultiplier = DamageModifier != null ? DamageModifier.Calc(player, damageMultiplier) : damageMultiplier;
        speed = SpeedModifier != null ? SpeedModifier.Calc(player, speed) : speed;
        int durabilityDamage = 0;

        foreach (ItemStack ammo in ammoStacks)
        {
            for (int count = 0; count < ammo.StackSize; count++)
            {
                Vec3d projectilePosition = ProjectilePosition(player, new Vec3f(0.0f, 0.0f, 0.0f));
                DirectionOffset dispersion = offset + OffsetGetter?.Invoke(player) ?? DirectionOffset.Zero;
                Vec3d projectileVelocity = ProjectileVelocity(player, dispersion, speed);
                durabilityDamage += SpawnProjectile(ammo, player, projectilePosition, projectileVelocity, damageMultiplier);
            }
        }

        return durabilityDamage;
    }
    private static Vec3d ProjectilePosition(IPlayer player, Vec3f muzzlePosition)
    {
        Vec3f worldPosition = Utils.FromCameraReferenceFrame(player.Entity, muzzlePosition);
        return player.Entity.SidedPos.AheadCopy(0).XYZ.Add(worldPosition.X, player.Entity.LocalEyePos.Y + worldPosition.Y, worldPosition.Z);
    }
    private static Vec3d ProjectileVelocity(IPlayer player, DirectionOffset dispersion, float speed)
    {
        Vec3d pos = player.Entity.ServerPos.XYZ.Add(0, player.Entity.LocalEyePos.Y, 0);
        Vec3d aheadPos = pos.AheadCopy(1, player.Entity.SidedPos.Pitch + dispersion.Pitch.Radians, player.Entity.SidedPos.Yaw + dispersion.Yaw.Radians);
        return (aheadPos - pos).Normalize() * speed;
    }
    private int SpawnProjectile(ItemStack projectileStack, IPlayer player, Vec3d position, Vec3d velocity, float damageMultiplier)
    {
        if (projectileStack?.Item?.Code == null) return 0;

        AdvancedProjectileBehavior? projectileStats = projectileStack.Collectible.GetBehavior<AdvancedProjectileBehavior>();
        if (projectileStats == null)
        {
            Logger.Warn(Api, this, $"Failed to retrieve damage types from '{projectileStack.Item.Code}'. Try adding FSMAdvancedProjectile behavior to projectile itemtype/blocktype.");
            return 0;
        }

        AssetLocation entityTypeAsset = new(projectileStack.Collectible.Attributes["projectile"].AsString(projectileStack.Collectible.Code.Path));
        EntityProperties entityType = Api.World.GetEntityType(entityTypeAsset);

        if (entityType == null)
        {
            Logger.Warn(Api, this, $"Failed to create entity '{entityTypeAsset}'.");
            return 0;
        }


        if (Api.ClassRegistry.CreateEntity(entityType) is not AdvancedEntityProjectile projectile)
        {
            Logger.Warn(Api, this, $"Entity '{entityTypeAsset}' should have 'AdvancedEntityProjectile' class or its subclass.");
            return 0;
        }

        projectile.FiredBy = player.Entity;
        projectile.ProjectileStack = projectileStack;
        projectile.DropOnImpactChance = 1 - projectileStack.Collectible.Attributes["breakChanceOnImpact"].AsFloat(0);
        projectile.ServerPos.SetPos(position);
        projectile.ServerPos.Motion.Set(velocity);
        projectile.Pos.SetFrom(projectile.ServerPos);
        projectile.World = Api.World;
        projectile.SetRotation();

        projectile.DamageTypes = projectileStats.DamageTypes;
        projectile.DamageMultiplier = damageMultiplier;
        projectile.HitSound = projectileStats.HitSound.ToString();
        projectile.ImpactSound = projectileStats.ImpactSound.ToString();

        Api.World.SpawnEntity(projectile);

        return projectileStats.AdditionalDurabilityCost;
    }
}