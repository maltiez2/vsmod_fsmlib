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
        private TransformsManager mTransformsManager;

        private ModelTransform DefaultFpHandTransform;
        private LoadedTexture AimTexPartCharge;
        private LoadedTexture AimTexFullCharge;
        private LoadedTexture AimTexBlocked;

        private float mAimDuration;
        private readonly Dictionary<long, Vec3d> mAimDirections = new();
        private readonly Dictionary<long, Utils.TickBasedTimer> mAimTimers = new();
        private readonly Dictionary<long, ModelTransform> mAimTransforms = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mTransformsManager = mCollectible.GetBehavior<FiniteStateMachineBehaviour>().transformsManager;

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
            if (!mAimTransforms.ContainsKey(player.EntityId)) mAimTransforms.Add(player.EntityId, Utils.IdentityTransform());

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    StartAiming(slot, player);
                    break;
                case "stop":
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
            return GetOffset(mAimDirections[player.EntityId], player);
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
            if (mAimTimers.ContainsKey(player.EntityId)) mAimTimers[player.EntityId]?.Stop();
            mAimTimers[player.EntityId] = new(mApi, (int)(mAimDuration * 1000), (float progress) => ProceedAiming(progress, player), false);

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
        private void ProceedAiming(float progress, EntityAgent byEntity)
        {
            if (mApi.Side != EnumAppSide.Client) return;

            if (byEntity.Attributes.GetInt("bullseyeAiming") == 0)
            {
                mApi.Logger.Notification("[FSMlib] mAimTransforms revert: {0}", mAimTransforms[byEntity.EntityId].Rotation);
                mTransformsManager.SetTransform(byEntity.EntityId, mCode, EnumItemRenderTarget.HandFp, Utils.TransitionTransform(Utils.IdentityTransform(), mAimTransforms[byEntity.EntityId], progress));
                return;
            }

            SetReticle(progress, byEntity);

            Vec3d direction = CoreClientSystem.TargetVec;
            mAimDirections[byEntity.EntityId] = direction;
            RangedWeaponSystem.SendRangedWeaponFirePacket(mCollectible.Id, direction);

            mAimTransforms[byEntity.EntityId] = Utils.TransitionTransform(Utils.IdentityTransform(), AimTransform(), progress * progress);
            mApi.Logger.Notification("[FSMlib] mAimTransforms: {0}", mAimTransforms[byEntity.EntityId].Rotation);
            mTransformsManager.SetTransform(byEntity.EntityId, mCode, EnumItemRenderTarget.HandFp, mAimTransforms[byEntity.EntityId]);
        }
        private void SetReticle(float progress, EntityAgent byEntity)
        {
            bool showBlocked = progress < 0.0;
            bool showPartCharged = progress < 1.0;

            CoreClientSystem.WeaponReadiness = showBlocked ? BullseyeEnumWeaponReadiness.Blocked : showPartCharged ? BullseyeEnumWeaponReadiness.PartCharge : BullseyeEnumWeaponReadiness.FullCharge;
        }
        private void CancelAiming(EntityAgent byEntity)
        {
            if (mAimTimers.ContainsKey(byEntity.EntityId)) mAimTimers[byEntity.EntityId]?.Revert(true);

            mTransformsManager.ResetTransform(byEntity.EntityId, mCode, EnumItemRenderTarget.HandFp);

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
            ModelTransform fpTransform = Utils.IdentityTransform();

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

            fpTransform.Translation.Y -= 0.5f; // @TODO

            return fpTransform;
        }
    }
}
