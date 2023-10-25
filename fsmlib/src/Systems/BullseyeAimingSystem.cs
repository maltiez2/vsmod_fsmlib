using Bullseye;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using MaltiezFSM.Framework;

namespace MaltiezFSM.Systems
{
    public class BullseyeAiming : BaseSystem, IAimingSystem
    {
        private BullseyeSystemClientAiming CoreClientSystem;
        private BullseyeSystemRangedWeapon RangedWeaponSystem;
        private BullseyeRangedWeaponStats WeaponStats;

        private ModelTransform DefaultFpHandTransform;
        private LoadedTexture AimTexPartCharge;
        private LoadedTexture AimTexFullCharge;
        private LoadedTexture AimTexBlocked;

        private float mAimDuration;
        private readonly Dictionary<long, Vec3d> mAimDirections = new();
        private readonly Dictionary<long, TickBasedTimer> mAimTimers = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            RangedWeaponSystem = api.ModLoader.GetModSystem<BullseyeSystemRangedWeapon>();

            DefaultFpHandTransform = collectible.FpHandTransform.Clone();

            WeaponStats = collectible.Attributes.KeyExists("bullseyeWeaponStats") ? collectible.Attributes?["bullseyeWeaponStats"].AsObject<BullseyeRangedWeaponStats>() : new BullseyeRangedWeaponStats();

            if (api.Side == EnumAppSide.Server)
            {
                mApi.Event.RegisterEventBusListener(ServerHandleFire, 0.5, "bullseyeRangedWeaponFire");
            }
            else
            {
                CoreClientSystem = api.ModLoader.GetModSystem<BullseyeSystemClientAiming>();
                AimTexPartCharge = new LoadedTexture((mApi as ICoreClientAPI));
                AimTexFullCharge = new LoadedTexture((mApi as ICoreClientAPI));
                AimTexBlocked = new LoadedTexture((mApi as ICoreClientAPI));

                if (WeaponStats?.aimTexPartChargePath != null) (mApi as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexPartChargePath), ref AimTexPartCharge);
                if (WeaponStats?.aimTexFullChargePath != null) (mApi as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexFullChargePath), ref AimTexFullCharge);
                if (WeaponStats?.aimTexBlockedPath != null) (mApi as ICoreClientAPI).Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexBlockedPath), ref AimTexBlocked);
            }

            mAimDuration = definition["duration"].AsFloat() / 1000;
        }
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            if (!mAimDirections.ContainsKey(player.EntityId)) mAimDirections.Add(player.EntityId, new Vec3d(0, 0, 0));

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    StartAiming(slot, player);
                    StartTimer(player);
                    break;
                case "stop":
                    StopTimer(player);
                    CancelAiming(player);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicAim] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }
        IAimingSystem.DirectionOffset IAimingSystem.GetShootingDirectionOffset(ItemSlot slot, EntityAgent player)
        {
            IAimingSystem.DirectionOffset offset = GetOffset(mAimDirections[player.EntityId], player);
            mApi.Logger.Notification("[FSMlib] [GetShootingDirectionOffset()] pitch: {0}, yaw: {1}", offset.pitch, offset.yaw);
            return offset;
        }

        private IAimingSystem.DirectionOffset GetOffset(Vec3d directionAbs, EntityAgent player)
        {
            Vec3d direction = Utils.ToCameraReferenceFrame(player, directionAbs);
            double pitchCosine = -direction.Y / GameMath.Sqrt(direction.Z * direction.Z + direction.Y * direction.Y);
            double yawCosine = -direction.X / GameMath.Sqrt(direction.Z * direction.Z + direction.X * direction.X);
            double pitch = GameMath.Asin(pitchCosine);
            double yaw = GameMath.Asin(yawCosine);
            
            return ((float)GameMath.Clamp(pitch, -GameMath.PI, GameMath.PI), (float)GameMath.Clamp(yaw, -GameMath.PI, GameMath.PI));
        }
        private void StartAiming(ItemSlot slot, EntityAgent player)
        {
            if (player.World is IClientWorldAccessor)
            {
                CoreClientSystem.SetRangedWeaponStats(WeaponStats);
                CoreClientSystem.SetReticleTextures(AimTexPartCharge, AimTexFullCharge, AimTexBlocked);
            }

            RangedWeaponSystem.SetLastEntityRangedChargeData(player.EntityId, slot);

            player.GetBehavior<BullseyeEntityBehaviorAimingAccuracy>().SetRangedWeaponStats(WeaponStats);

            player.Attributes.SetInt("bullseyeAiming", 1);
            player.Attributes.SetInt("bullseyeAimingCancel", 0);
        }
        private void ProceedAiming(float secondsUsed, EntityAgent byEntity)
        {
            if (mApi.Side != EnumAppSide.Client || byEntity.Attributes.GetInt("bullseyeAiming") != 1) return;

            SetReticle(secondsUsed, byEntity);

            Vec3d direction = CoreClientSystem.TargetVec;
            mAimDirections[byEntity.EntityId] = direction;
            RangedWeaponSystem.SendRangedWeaponFirePacket(mCollectible.Id, direction);

            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = AimTransform(); // Utils.CombineTransforms(mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform, AimTransform());
        }
        private void SetReticle(float secondsUsed, EntityAgent byEntity)
        {
            // Show different reticle if we are ready to shoot
            // - Show white "full charge" reticle if the accuracy is fully calmed down, + a little leeway to let the reticle calm down fully
            // - Show yellow "partial charge" reticle if the bow is ready for a snap shot, but accuracy is still poor
            // --- OR if the weapon was held so long that accuracy is starting to get bad again, for weapons that have it
            // - Show red "blocked" reticle if the weapon can't shoot yet
            bool showBlocked = secondsUsed < mAimDuration;
            bool showPartCharged = secondsUsed < WeaponStats.accuracyStartTime / byEntity.Stats.GetBlended("rangedWeaponsSpeed") + WeaponStats.aimFullChargeLeeway;
            showPartCharged = showPartCharged || secondsUsed > WeaponStats.accuracyOvertimeStart + WeaponStats.accuracyStartTime && WeaponStats.accuracyOvertime > 0;

            CoreClientSystem.WeaponReadiness = showBlocked ? BullseyeEnumWeaponReadiness.Blocked : showPartCharged ? BullseyeEnumWeaponReadiness.PartCharge : BullseyeEnumWeaponReadiness.FullCharge;
        }
        private void CancelAiming(EntityAgent byEntity)
        {
            if (byEntity.Attributes.GetInt("bullseyeAimingCancel") == 1) return;
            byEntity.Attributes.SetInt("bullseyeAimingCancel", 1);
            byEntity.Attributes.SetInt("bullseyeAiming", 0);
        }
        private void ServerHandleFire(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            int itemId = tree.GetInt("itemId");

            if (itemId == mCollectible.Id)
            {
                long entityId = tree.GetLong("entityId");

                ItemSlot itemSlot = RangedWeaponSystem.GetLastEntityRangedItemSlot(entityId);
                EntityAgent byEntity = mApi.World.GetEntityById(entityId) as EntityAgent;

                if (RangedWeaponSystem.GetEntityChargeStart(entityId) + mAimDuration < mApi.World.ElapsedMilliseconds / 1000f && byEntity.Alive && itemSlot != null)
                {
                    mAimDirections[byEntity.EntityId] = new Vec3d(tree.GetDouble("aimX"), tree.GetDouble("aimY"), tree.GetDouble("aimZ"));
                    handling = EnumHandling.PreventSubsequent;
                }
            }
        }
        private ModelTransform AimTransform()
        {
            ModelTransform fpTransform = new();
            fpTransform.EnsureDefaultValues();

            float transformFraction;

            if (!RangedWeaponSystem.HasEntityCooldownPassed((mApi as ICoreClientAPI).World.Player.Entity.EntityId, WeaponStats.cooldownTime))
            {
                float cooldownRemaining = WeaponStats.cooldownTime - RangedWeaponSystem.GetEntityCooldownTime((mApi as ICoreClientAPI).World.Player.Entity.EntityId);
                float transformTime = 0.25f;

                transformFraction = WeaponStats.weaponType != BullseyeRangedWeaponType.Throw ?
                    GameMath.Clamp((WeaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f) : 1f;
                transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
            }
            else
            {
                transformFraction = 0;
            }

            fpTransform.Translation.Y = DefaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);

            if (CoreClientSystem.Aiming)
            {
                Vec2f currentAim = CoreClientSystem.GetCurrentAim();

                fpTransform.Rotation.X = DefaultFpHandTransform.Rotation.X - (currentAim.Y / 15f);
                fpTransform.Rotation.Y = DefaultFpHandTransform.Rotation.Y - (currentAim.X / 15f);
            }
            else
            {
                fpTransform.Rotation.Set(DefaultFpHandTransform.Rotation);
            }

            return fpTransform;
        }
        private void StartTimer(EntityAgent player)
        {
            StopTimer(player);
            mAimTimers[player.EntityId] = new TickBasedTimer(mApi, (int)(mAimDuration * 1000), (float progress) => ProceedAiming(mAimDuration * progress, player), false);
        }
        private void StopTimer(EntityAgent player)
        {
            if (mAimTimers.ContainsKey(player.EntityId)) mAimTimers[player.EntityId]?.Stop();
        }
    }
}
