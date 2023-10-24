using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using System.Collections.Generic;
using System;
using MaltiezFSM.Framework;
using Vintagestory.API.Common.Entities;
using System.Diagnostics;

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

        private readonly Dictionary<string, JsonObject> mFpAnimations = new();
        private readonly Dictionary<string, JsonObject> mTpAnimations = new();
        private readonly Dictionary<string, int> mDurations = new();
        private readonly Dictionary<long, TickBasedPlayerAnimation> mTimers = new();
        private ModelTransform mFpInitialTransform;
        private ModelTransform mTpInitialTransform;
        private ModelTransform mFpTargetTransform;
        private ModelTransform mTpTargetTransform;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            JsonObject[] animations = definition[animationsAttrName].AsArray();
            foreach (JsonObject animation in animations)
            {
                string animationCode = animation[codeAttrName].AsString();
                mDurations.Add(animationCode, animation[durationAttrName].AsInt());
                mFpAnimations.Add(animationCode, animation[fpAnimationAttrName]);
                mTpAnimations.Add(animationCode, animation[tpAnimationAttrName]);
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

            if (!mTimers.ContainsKey(player.EntityId)) mTimers[player.EntityId] = new();

            mTimers[player.EntityId]?.Stop();
            mTimers[player.EntityId]?.Init(mApi, duration, (float progress) => PlayAnimation(progress, player));

            ModelTransform fpLastTransform = mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform;
            ModelTransform tpLastTransform = mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform;
            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();

            switch (mode)
            {
                case "forward":
                    mFpInitialTransform = fpLastTransform != null ? fpLastTransform.Clone() : modelTransform.Clone();
                    mTpInitialTransform = tpLastTransform != null ? tpLastTransform.Clone() : modelTransform.Clone();
                    mFpTargetTransform = Utils.ToTransformFrom(mFpAnimations[code]);
                    mTpTargetTransform = Utils.ToTransformFrom(mTpAnimations[code]);
                    mTimers[player.EntityId]?.Play();
                    break;
                case "backward":
                    mFpInitialTransform = mFpTargetTransform.Clone();
                    mTpInitialTransform = mTpTargetTransform.Clone();
                    mFpTargetTransform = modelTransform.Clone();
                    mTpTargetTransform = modelTransform.Clone();
                    mTimers[player.EntityId]?.Play();
                    break;
                case "cancel":
                    player.Controls.UsingHeldItemTransformAfter = modelTransform.Clone();
                    player.Controls.UsingHeldItemTransformBefore = modelTransform.Clone();
                    mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = modelTransform.Clone();
                    mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = modelTransform.Clone();
                    mTimers[player.EntityId]?.Stop();
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicTransformAnimation] [Process] Mode does not exists: " + mode);
                    return false;
            }
            
            return true;
        }

        private void PlayAnimation(float progress, EntityAgent player)
        {
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = Utils.TransitionTransform(mFpInitialTransform, mFpTargetTransform, progress);
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = Utils.TransitionTransform(mTpInitialTransform, mTpInitialTransform, progress);
            ResetUsingTransforms(player);
        }

        private void ResetUsingTransforms(EntityAgent player)
        {
            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();
            player.Controls.UsingHeldItemTransformAfter = modelTransform.Clone();
            player.Controls.UsingHeldItemTransformBefore = modelTransform.Clone();
        }
    }

    public class TickBasedPlayerAnimation
    {
        public const int listenerDelay = 0;

        private ICoreAPI mApi;
        private long? mCallbackId;
        private bool mForward = true;
        private Action<float> mCallback;
        private float mDuration_ms;
        private float mCurrentDuration;
        private float mCurrentProgress;

        public void Init(ICoreAPI api, int duration_ms, Action<float> callback)
        {
            mDuration_ms = (float)duration_ms / 1000;
            mApi = api;
            mCallback = callback;
        }
        public void Play()
        {
            mCurrentDuration = 0;
            mCurrentProgress = 0;
            mForward = true;
            SetAnimation(0);
            SetListener();
        }
        public void Handler(float time)
        {
            mCurrentDuration += time;
            SetAnimation(CalculateProgress(mCurrentDuration));
            if (mCurrentDuration >= mDuration_ms) StopListener();
        }
        public void Stop()
        {
            StopListener();
        }
        public void Revert()
        {
            mCurrentDuration = mDuration_ms * (1 - mCurrentProgress);
            mForward = false;
            SetAnimation(CalculateProgress(mCurrentDuration));
            SetListener();
        }

        private float CalculateProgress(float time)
        {
            float progress = time / mDuration_ms;
            progress = progress > 1 ? 1 : progress;
            mCurrentProgress = mForward ? progress : 1 - progress;
            return mCurrentProgress;
        }
        private void SetAnimation(float progress)
        {
            mCallback(progress);
        }
        private void SetListener()
        {
            StopListener();
            
            mCallbackId = mApi.World.RegisterGameTickListener(Handler, listenerDelay);
        }
        private void StopListener()
        {
            if (mCallbackId != null) mApi.World.UnregisterGameTickListener((long)mCallbackId);
            mCallbackId = null;
        }
    }
}
