using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using System.Collections.Generic;
using System;
using MaltiezFSM.Framework;
using Vintagestory.API.Client;
using MaltiezFSM.API;

namespace MaltiezFSM.Systems
{
    public class BasicTransformAnimation : BaseSystem, ITranformAnimationSystem
    {
        public const string animationsAttrName = "animations";
        public const string durationAttrName = "duration";
        public const string codeAttrName = "code";
        public const string modeAttrName = "mode";
        public const string fpAnimationAttrName = "fpTransform";
        public const string tpAnimationAttrName = "tpTransform";

        private readonly Dictionary<string, ModelTransform> mFpAnimations = new();
        private readonly Dictionary<string, ModelTransform> mTpAnimations = new();
        private readonly Dictionary<string, int> mDurations = new();
        private readonly Dictionary<long, Utils.TickBasedTimer> mTimers = new();
        private readonly Dictionary<long, PlayerHeldItemTransformManager> mTransformManagers = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            JsonObject[] animations = definition[animationsAttrName].AsArray();
            foreach (JsonObject animation in animations)
            {
                string animationCode = animation[codeAttrName].AsString();
                mDurations.Add(animationCode, animation[durationAttrName].AsInt());
                
                if (animation.KeyExists(fpAnimationAttrName))
                {
                    mFpAnimations.Add(animationCode, Utils.ToTransformFrom(animation[fpAnimationAttrName]));
                }
                else
                {
                    mFpAnimations.Add(animationCode, null);
                }

                if (animation.KeyExists(tpAnimationAttrName))
                {
                    mTpAnimations.Add(animationCode, Utils.ToTransformFrom(animation[tpAnimationAttrName]));
                }
                else
                {
                    mTpAnimations.Add(animationCode, null);
                }
            }
        }
        public override bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Verify(slot, player, parameters)) return false;

            string code = parameters[codeAttrName].AsString();

            if (!mFpAnimations.ContainsKey(code)) mApi.Logger.Error("[FSMlib] [BasicTransformAnimation] [Verify] No animations with code '" + code + "' are defined");

            return mFpAnimations.ContainsKey(code);
        }
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string code = parameters[codeAttrName].AsString();
            string mode = parameters[modeAttrName].AsString();
            int duration = mDurations[code];
            if (parameters.KeyExists(durationAttrName)) duration = parameters[durationAttrName].AsInt();

            return ProcessImpl(slot, player, null, code, mode, duration, ProgressModifiers.Linear);
        }

        private bool ProcessImpl(ItemSlot slot, EntityAgent player, Action finishCallback, string code, string mode, int duration, ProgressModifiers.ProgressModifier progressModifier)
        {
            if (!mFpAnimations.ContainsKey(code)) return false;
            if (!mTimers.ContainsKey(player.EntityId)) mTimers[player.EntityId] = null;
            if (!mTransformManagers.ContainsKey(player.EntityId)) mTransformManagers[player.EntityId] = new(player.EntityId, mCode, mCollectible);

            mTimers[player.EntityId]?.Stop();

            switch (mode)
            {
                case "forward":
                    mTransformManagers[player.EntityId].StartForward(slot, mFpAnimations[code], mTpAnimations[code], mCollectible);
                    mTimers[player.EntityId] = new(mApi, TimeSpan.FromMilliseconds(duration), (float progress) => PlayAnimation(progressModifier(progress), player, finishCallback));
                    break;
                case "backward":
                    mTransformManagers[player.EntityId].StartBackward(slot);
                    mTimers[player.EntityId]?.Revert(duration);
                    break;
                case "cancel":
                    mTransformManagers[player.EntityId].Cancel(player);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicTransformAnimation] [Process] Mode does not exists: " + mode);
                    return false;
            }

            return true;
        }

        private void PlayAnimation(float progress, EntityAgent player, Action finishCallback)
        {
            mTransformManagers[player.EntityId].Play(progress, player);
            if (finishCallback != null && progress >= 1.0) finishCallback();
        }

        void ITranformAnimationSystem.PlayAnimation(ItemSlot slot, EntityAgent player, ITranformAnimationSystem.AnimationData animationData, Action finishCallback, string mode)
        {
            int duration = mDurations[animationData.code];
            if (animationData.duration != null) duration = (int)animationData.duration;
            ProcessImpl(slot, player, finishCallback, animationData.code, mode, duration, animationData.dynamic);
        }
    }

    public class PlayerHeldItemTransformManager
    {
        private readonly ITransformManager mTransformsManager;
        private readonly long mEntityId;
        private readonly string mCode;

        private ModelTransform mFpInitialTransform = Utils.IdentityTransform();
        private ModelTransform mTpInitialTransform = Utils.IdentityTransform();
        private ModelTransform mFpTargetTransform = Utils.IdentityTransform();
        private ModelTransform mTpTargetTransform = Utils.IdentityTransform();
        private float mCurrentProgress;

        private bool fpAnimation;
        private bool tpAnimation;

        public PlayerHeldItemTransformManager(long entityId, string code, CollectibleObject collectible)
        {
            mEntityId = entityId;
            mCode = code;

            foreach (CollectibleBehavior behavior in collectible.CollectibleBehaviors)
            {
                if (behavior is ITransformManagerProvider)
                {
                    mTransformsManager = (behavior as ITransformManagerProvider).GetTransformManager();
                    break;
                }
            }
        }

        public void StartForward(ItemSlot slot, ModelTransform fpTransform, ModelTransform tpTransform, CollectibleObject collectible)
        {
            fpAnimation = fpTransform != null;
            tpAnimation = tpTransform != null;

            if (slot != null)
            {
                mTransformsManager.SetEntityId(mEntityId, slot.Itemstack);
                slot.MarkDirty();
            }

            if (fpAnimation)
            {
                ModelTransform fpLastTransform = mTransformsManager.GetTransform(mEntityId, mCode, EnumItemRenderTarget.HandFp);
                mFpInitialTransform = fpLastTransform != null ? fpLastTransform : Utils.IdentityTransform();
                mFpTargetTransform = Utils.SubtractTransformsNoScale(fpTransform, collectible.FpHandTransform);
            }

            if (tpAnimation)
            {
                ModelTransform tpLastTransform = mTransformsManager.GetTransform(mEntityId, mCode, EnumItemRenderTarget.HandTp);
                mTpInitialTransform = tpLastTransform != null ? tpLastTransform : Utils.IdentityTransform();
                mTpTargetTransform = Utils.SubtractTransformsNoScale(tpTransform, collectible.TpHandTransform);
            }
        }

        public void StartBackward(ItemSlot slot)
        {
            if (slot != null)
            {
                mTransformsManager.SetEntityId(mEntityId, slot.Itemstack);
                slot.MarkDirty();
            }

            if (tpAnimation)
            {
                mTpInitialTransform = Utils.IdentityTransform();
            }

            if (fpAnimation)
            {
                ModelTransform currentFpTransform = Utils.TransitionTransform(mFpInitialTransform, mFpTargetTransform, mCurrentProgress);
                mFpInitialTransform = Utils.IdentityTransform();
                if (mCurrentProgress <= 0) return;
                mFpTargetTransform = Utils.TransitionTransform(mFpInitialTransform, currentFpTransform, 1 / mCurrentProgress);
            }
        }

        public void Cancel(EntityAgent player)
        {
            player.Controls.UsingHeldItemTransformAfter = Utils.IdentityTransform();
            player.Controls.UsingHeldItemTransformBefore = Utils.IdentityTransform();
            if (fpAnimation) mTransformsManager.ResetTransform(mEntityId, mCode, EnumItemRenderTarget.HandFp);
            if (tpAnimation) mTransformsManager.ResetTransform(mEntityId, mCode, EnumItemRenderTarget.HandTp);
        }

        public void Play(float progress, EntityAgent player)
        {
            mCurrentProgress = progress;
            if (fpAnimation) mTransformsManager.SetTransform(mEntityId, mCode, EnumItemRenderTarget.HandFp, Utils.TransitionTransform(mFpInitialTransform, mFpTargetTransform, progress));
            if (tpAnimation) mTransformsManager.SetTransform(mEntityId, mCode, EnumItemRenderTarget.HandTp, Utils.TransitionTransform(mTpInitialTransform, mTpTargetTransform, progress));
            player.Controls.UsingHeldItemTransformAfter = Utils.IdentityTransform();
            player.Controls.UsingHeldItemTransformBefore = Utils.IdentityTransform();
        }
    }
}
