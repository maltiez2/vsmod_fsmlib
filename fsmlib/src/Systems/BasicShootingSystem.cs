using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MaltiezFSM.Systems
{
    public class BasicShooting : BaseSystem
    {
        public const string ammoSelectorSystemAttrName = "reloadSystem";
        public const string aimingSystemAttrName = "aimingSystem";
        public const string velocityAttrName = "projectileVelocity";
        public const string damageAttrName = "projectileDamage";
        public const string damageMultiplierAttrName = "projectileDamageMultiplier";
        public const string descriptionAttrName = "description";

        private string mReloadSystemName;
        private readonly List<string> mAimingSystemsName = new();
        private string mDescription;
        private IAmmoSelector mReloadSystem;
        private readonly List<IAimingSystem> mAimingSystems = new();
        private float mProjectileVelocity;
        private float mProjectileDamage;
        private float mProjectileDamageMultiplier;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mReloadSystemName = definition[ammoSelectorSystemAttrName].AsString();
            mProjectileVelocity = definition[velocityAttrName].AsFloat(1);
            mProjectileDamage = definition[damageAttrName].AsFloat(0);
            mProjectileDamageMultiplier = definition[damageMultiplierAttrName].AsFloat(0);
            mDescription = definition[descriptionAttrName].AsString();
            
            if (definition[aimingSystemAttrName].IsArray())
            {
                foreach (var item in definition[aimingSystemAttrName].AsArray())
                {
                    mAimingSystemsName.Add(item.AsString());
                }
            }
            else
            {
                mAimingSystemsName.Add(definition[aimingSystemAttrName].AsString());
            }
        }
        public override void SetSystems(Dictionary<string, ISystem> systems)
        {
            mReloadSystem = systems[mReloadSystemName] as IAmmoSelector;
            foreach (var item in mAimingSystemsName)
            {
                mAimingSystems.Add(systems[item] as IAimingSystem);
            }
        }

        public override bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Verify(slot, player, parameters)) return false;

            return mReloadSystem.GetSelectedAmmo(slot) != null;
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            ItemStack ammoStack = mReloadSystem.TakeSelectedAmmo(slot);
            if (ammoStack == null) return false;

            Vec3d projectilePosition = ProjectilePosition(player, new Vec3f(0.0f, 0.0f, 0.0f));
            IAimingSystem.DirectionOffset dispersion = (0f, 0f);
            foreach (var item in mAimingSystems) 
            {
                IAimingSystem.DirectionOffset offset = item.GetShootingDirectionOffset(slot, player);
                dispersion.pitch += offset.pitch;
                dispersion.yaw += offset.yaw;
            }
            Vec3d projectileVelocity = ProjectileVelocity(player, dispersion);

            float damage = mProjectileDamage;
            if (ammoStack.Collectible != null) damage += mProjectileDamageMultiplier * ammoStack.Collectible.Attributes["damage"].AsFloat();

            SpawnProjectile(ammoStack, player, projectilePosition, projectileVelocity, damage);

            return true;
        }
        public override string[] GetDescription(ItemSlot slot, IWorldAccessor world)
        {
            if (mDescription == null) return System.Array.Empty<string>();

            return new string[] { Lang.Get(mDescription, mProjectileVelocity, mProjectileDamage, mProjectileDamageMultiplier) };
        }


        private Vec3d ProjectilePosition(EntityAgent player, Vec3f muzzlePosition)
        {
            Vec3f worldPosition = Utils.FromCameraReferenceFrame(player, muzzlePosition);
            return player.SidedPos.AheadCopy(0).XYZ.Add(worldPosition.X, player.LocalEyePos.Y + worldPosition.Y, worldPosition.Z);
        }
        private Vec3d ProjectileVelocity(EntityAgent player, IAimingSystem.DirectionOffset dispersion)
        {
            Vec3d pos = player.ServerPos.XYZ.Add(0, player.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, player.SidedPos.Pitch + dispersion.pitch, player.SidedPos.Yaw + dispersion.yaw);
            return (aheadPos - pos).Normalize() * mProjectileVelocity;
        }
        private void SpawnProjectile(ItemStack projectileStack, EntityAgent player, Vec3d position, Vec3d velocity, float damage)
        {
            if (projectileStack?.Item?.Code == null) return;

            EntityProperties type = player.World.GetEntityType(projectileStack.Item.Code);
            var projectile = player.World.ClassRegistry.CreateEntity(type) as EntityProjectile;
            projectile.FiredBy = player;
            projectile.Damage = damage;
            projectile.ProjectileStack = projectileStack;
            projectile.DropOnImpactChance = 1 - projectileStack.Collectible.Attributes["breakChanceOnImpact"].AsFloat(0);
            projectile.ServerPos.SetPos(position);
            projectile.ServerPos.Motion.Set(velocity);
            projectile.Pos.SetFrom(projectile.ServerPos);
            projectile.World = player.World;
            projectile.SetRotation();

            player.World.SpawnEntity(projectile);
        }
    }
}
