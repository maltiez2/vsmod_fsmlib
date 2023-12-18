using AnimationManagerLib.API;
using AnimationManagerLib.CollectibleBehaviors;
using AnimationManagerLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace MaltiezFSM.Systems
{
    public class ItemAnimation : BaseSystem, IAnimationSystem
    {
        private IAnimationManagerSystem mAnimationManager;
        private readonly Dictionary<(string category, string animation), AnimationRequest[]> mAnimations = new();
        private readonly Dictionary<string, Category> mCategories = new();
        private readonly Dictionary<(long entityId, string animation, string category), Guid> mStartedAnimations = new();
        private Animatable mBehavior;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);
            if (mApi.Side != EnumAppSide.Client) return;

            mAnimationManager = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

            if (!mCollectible.HasBehavior<Animatable>())
            {
                mCollectible.CollectibleBehaviors = mCollectible.CollectibleBehaviors.Append(new Animatable(collectible));
                mCollectible.CollectibleBehaviors[^1].Initialize(definition["parameters"]);
                mCollectible.CollectibleBehaviors[^1].OnLoaded(api);
            }

            mBehavior = mCollectible.GetBehavior<Animatable>();
            mBehavior.RenderProceduralAnimations = true;

            foreach (JsonObject category in definition["categories"].AsArray())
            {
                string categoryCode = category["code"].AsString();
                mCategories.Add(categoryCode, Utils.CategoryFromJson(category));
            }

            foreach (JsonObject animation in definition["animations"].AsArray())
            {
                string animationCode = animation["code"].AsString();

                foreach ((string categoryName, _) in mCategories)
                {
                    ParseAnimations(animation["stages"].AsArray(), categoryName, animationCode);
                }
            }
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            if (mApi.Side != EnumAppSide.Client) return true;

            string code = parameters["code"].AsString();
            string category = parameters["category"].AsString();
            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    PlayAnimation(code, category, player.EntityId);
                    break;
                case "stop":
                    StopAnimation(code, category, player.EntityId);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [PlayerAnimation] [Process] Type does not exists: " + action);
                    return false;
            }

            return true;
        }

        void IAnimationSystem.PlayAnimation(ItemSlot slot, EntityAgent player, string code, string category, string action)
        {
            switch (action)
            {
                case "start":
                    PlayAnimation(code, category, player.EntityId);
                    break;
                case "stop":
                    StopAnimation(code, category, player.EntityId);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [PlayerAnimation] [PlayAnimation] Type does not exists: " + action);
                    break;
            }
        }

        private void PlayAnimation(string code, string category, long entityId)
        {
            Guid runId = mAnimationManager.Run(AnimationTarget.HeldItem(), mAnimations[(category, code)]);
            mStartedAnimations[(entityId, code, category)] = runId;
        }

        private void StopAnimation(string code, string category, long entityId)
        {
            if (mStartedAnimations.ContainsKey((entityId, code, category))) mAnimationManager.Stop(mStartedAnimations[(entityId, code, category)]);
        }

        private void ParseAnimations(JsonObject[] animationsDefinitions, string category, string animation)
        {
            List<AnimationRequest> requests = new();
            foreach (JsonObject requestDefinition in animationsDefinitions)
            {
                string animationCode = requestDefinition["code"].AsString();
                AnimationId animationId = new(category, animationCode);
                RunParameters? parameters = Utils.RunParametersFromJson(requestDefinition, out string errorMessage);
                if (parameters == null)
                {
                    mApi.Logger.Error("[FSMlib] [PlayerAnimation] [Init] Error on parsing animations: {0}", errorMessage);
                    return;
                }
                AnimationRequest request = new(animationId, parameters.Value);
                requests.Add(request);
                var animationData = AnimationManagerLib.API.AnimationData.HeldItem(animationCode, mBehavior.CurrentShape);
                mAnimationManager.Register(animationId, animationData);
            }

            mAnimations.Add((category, animation), requests.ToArray());
        }
    }
}
