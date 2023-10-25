using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using System.Collections.Generic;
using System;
using MaltiezFSM.Framework;
using Vintagestory.API.Common.Entities;
using System.Diagnostics;
using HarmonyLib;
using Vintagestory.API.Client;

namespace MaltiezFSM.Systems
{
    public class BasicTransformAnimation : BaseSystem
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
                mFpAnimations.Add(animationCode, Utils.ToTransformFrom(animation[fpAnimationAttrName]));
                mTpAnimations.Add(animationCode, Utils.ToTransformFrom(animation[tpAnimationAttrName]));
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

            if (!mFpAnimations.ContainsKey(code)) return false;

            if (!mTimers.ContainsKey(player.EntityId)) mTimers[player.EntityId] = null;
            if (!mTransformManagers.ContainsKey(player.EntityId)) mTransformManagers[player.EntityId] = new(player.EntityId, mCode, mCollectible);

            mTimers[player.EntityId]?.Stop();

            switch (mode)
            {
                case "forward":
                    mTransformManagers[player.EntityId].StartForward(mFpAnimations[code], mTpAnimations[code]);
                    mTimers[player.EntityId] = new(mApi, duration, (float progress) => PlayAnimation(progress, player));
                    break;
                case "backward":
                    mTransformManagers[player.EntityId].StartBackward();
                    mTimers[player.EntityId]?.Revert();
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

        private void PlayAnimation(float progress, EntityAgent player)
        {
            mTransformManagers[player.EntityId].Play(progress, player);
        }
    }

    public class PlayerHeldItemTransformManager
    {
        private readonly TransformsManager mTransformsManager;
        private readonly long mEntityId;
        private readonly string mCode;

        private ModelTransform mFpInitialTransform;
        private ModelTransform mTpInitialTransform;
        private ModelTransform mFpTargetTransform;
        private ModelTransform mTpTargetTransform;
        private float mCurrentProgress;
        

        public PlayerHeldItemTransformManager(long entityId, string code, CollectibleObject collectible)
        {
            mEntityId = entityId;
            mCode = code;
            mTransformsManager = collectible.GetBehavior<FiniteStateMachineBehaviour>().transformsManager;
        }

        public void StartForward(ModelTransform fpTransform, ModelTransform tpTransform)
        {
            ModelTransform fpLastTransform = mTransformsManager.GetTransform(mEntityId, mCode, EnumItemRenderTarget.HandFp);
            ModelTransform tpLastTransform = mTransformsManager.GetTransform(mEntityId, mCode, EnumItemRenderTarget.HandTp);
            mFpInitialTransform = fpLastTransform != null ? fpLastTransform.Clone() : Utils.IdentityTransform();
            mTpInitialTransform = tpLastTransform != null ? tpLastTransform.Clone() : Utils.IdentityTransform();
            mFpTargetTransform = fpTransform;
            mTpTargetTransform = tpTransform;
        }

        public void StartBackward()
        {
            ModelTransform currentFpTransform = Utils.TransitionTransform(mFpInitialTransform, mFpTargetTransform, mCurrentProgress);
            
            mFpInitialTransform = Utils.IdentityTransform();
            mTpInitialTransform = Utils.IdentityTransform();

            if (mCurrentProgress <= 0) return;
            mFpTargetTransform = Utils.TransitionTransform(mFpInitialTransform, currentFpTransform, 1 / mCurrentProgress);
        }

        public void Cancel(EntityAgent player)
        {
            player.Controls.UsingHeldItemTransformAfter = Utils.IdentityTransform();
            player.Controls.UsingHeldItemTransformBefore = Utils.IdentityTransform();
            mTransformsManager.ResetTransform(mEntityId, mCode, EnumItemRenderTarget.HandFp);
            mTransformsManager.ResetTransform(mEntityId, mCode, EnumItemRenderTarget.HandTp);
        }

        public void Play(float progress, EntityAgent player)
        {
            mCurrentProgress = progress;
            mTransformsManager.SetTransform(mEntityId, mCode, EnumItemRenderTarget.HandFp, Utils.TransitionTransform(mFpInitialTransform, mFpTargetTransform, progress));
            mTransformsManager.SetTransform(mEntityId, mCode, EnumItemRenderTarget.HandTp, Utils.TransitionTransform(mTpInitialTransform, mTpTargetTransform, progress));
            player.Controls.UsingHeldItemTransformAfter = Utils.IdentityTransform();
            player.Controls.UsingHeldItemTransformBefore = Utils.IdentityTransform();
        }
    }
}
