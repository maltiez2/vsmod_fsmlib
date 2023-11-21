﻿using System;
using Vintagestory.API.Common;
using AnimationManagerLib.API;

namespace AnimationManagerLib
{
    public class PlayerModelAnimator<TAnimationResult> : IAnimator<TAnimationResult>
        where TAnimationResult : IAnimationResult
    {
        private TimeSpan mCurrentTime;
        private TAnimationResult mLastFrame;
        private TAnimationResult mStartFrame;
        private TAnimationResult mDefaultFrame;
        private IAnimation<TAnimationResult> mCurrentAnimation;
        private AnimationRunMetadata mCurrentParameters;
        private bool mStopped;

        void IAnimator<TAnimationResult>.Init(ICoreAPI api, TAnimationResult defaultFrame)
        {
            mDefaultFrame = (TAnimationResult)defaultFrame.Clone();
            mStartFrame = mDefaultFrame;
            mLastFrame = mDefaultFrame;
        }

        void IAnimator<TAnimationResult>.Run(AnimationRunMetadata parameters, IAnimation<TAnimationResult> animation)
        {
            mCurrentAnimation = animation;
            mCurrentParameters = parameters;
            mStartFrame = mLastFrame;
            mStopped = false;
        }

        TAnimationResult IAnimator<TAnimationResult>.Calculate(TimeSpan timeElapsed)
        {
            mCurrentTime += timeElapsed;

            if (mStopped) return mLastFrame;
            
            double progress = mCurrentTime.TotalSeconds / mCurrentParameters.Duration.TotalSeconds;

            switch (mCurrentParameters.Action)
            {
                case AnimationPlayerAction.Set:
                    mLastFrame = mCurrentAnimation.Play(1.0f, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    mStopped = true;
                    break;
                case AnimationPlayerAction.EaseIn:
                    mLastFrame = mCurrentAnimation.Blend(1f - (float)progress, mCurrentParameters.StartFrame, mStartFrame);
                    break;
                case AnimationPlayerAction.EaseOut:
                    mLastFrame = mCurrentAnimation.EaseOut((float)progress, mStartFrame);
                    break;
                case AnimationPlayerAction.Start:
                    mLastFrame = mCurrentAnimation.Play((float)progress, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    break;
                case AnimationPlayerAction.Stop:
                    mStopped = true;
                    break;
                case AnimationPlayerAction.Rewind:
                    mLastFrame = mCurrentAnimation.Play(1 - (float)progress, mCurrentParameters.StartFrame, mCurrentParameters.EndFrame);
                    break;
                case AnimationPlayerAction.Clear:
                    mLastFrame = mDefaultFrame;
                    mStopped = true;
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (progress >= 1.0) mStopped = true;

            return mLastFrame;
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
