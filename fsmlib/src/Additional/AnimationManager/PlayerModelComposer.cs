using System;
using AnimationManagerLib.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace AnimationManagerLib
{
    public class PlayerModelComposer<TAnimationResult> : IAnimationComposer<TAnimationResult>
        where TAnimationResult : IAnimationResult
    {
        private Type mAnimatorType;
        private readonly Dictionary<AnimationIdentifier, IAnimation<TAnimationResult>> mAnimations = new();
        private readonly Dictionary<CategoryIdentifier, IAnimator<TAnimationResult>> mAnimators = new();
        private readonly TAnimationResult mDefaultFrame;
        private readonly ICoreAPI mApi;

        public PlayerModelComposer(ICoreAPI api, TAnimationResult defaultFrame)
        {
            mApi = api;
            mDefaultFrame = defaultFrame;
        }
        void IAnimationComposer<TAnimationResult>.SetAnimatorType<TAnimator>() => mAnimatorType = typeof(TAnimator);
        bool IAnimationComposer<TAnimationResult>.Register(AnimationIdentifier id, IAnimation<TAnimationResult> animation) => mAnimations.TryAdd(id, animation);
        void IAnimationComposer<TAnimationResult>.Run(AnimationRequest request) => TryAddAnimator(request).Run(request, mAnimations[request]);

        TAnimationResult IAnimationComposer<TAnimationResult>.Compose(ComposeRequest request, TimeSpan timeElapsed)
        {
            TAnimationResult sum = mDefaultFrame;
            TAnimationResult average = mDefaultFrame;
            float averageWeight = 1;

            foreach ((var category, var animator) in mAnimators)
            {
                switch (category.Blending)
                {
                    case BlendingType.Average:
                        float weight = category.Weight == null ? 1 : (float)category.Weight;
                        average.Average(animator.Calculate(timeElapsed), averageWeight, weight);
                        averageWeight += weight;
                        break;
                    case BlendingType.Add:
                        sum.Add(animator.Calculate(timeElapsed));
                        break;
                    case BlendingType.Subtract:
                        throw new NotImplementedException();
                    default:
                        throw new NotImplementedException();
                }
            }

            return (TAnimationResult)average.Add(sum);
        }

        void IDisposable.Dispose()
        {
            throw new NotImplementedException();
        }

        private IAnimator<TAnimationResult> TryAddAnimator(AnimationRequest request)
        {
            if (mAnimators.ContainsKey(request)) return mAnimators[request];
            IAnimator<TAnimationResult> animator = Activator.CreateInstance(mAnimatorType) as IAnimator<TAnimationResult>;
            animator.Init(mApi, mDefaultFrame);
            mAnimators.Add(request, animator);
            return animator;
        }
    }
}
