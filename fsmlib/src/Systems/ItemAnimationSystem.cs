using AnimationManagerLib.API;
using AnimationManagerLib.CollectibleBehaviors;
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
        private AnimationManagerLib.API.IAnimationManager mAnimationManager;
        private readonly Dictionary<(string category, string animation), AnimationRequest[]> mAnimations = new();
        private readonly Dictionary<string, CategoryId> mCategories = new();
        private readonly Dictionary<(long entityId, string animation, string category), Guid> mStartedAnimations = new();
        private bool mClientSide;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mClientSide = api.Side == EnumAppSide.Client;
            if (!mClientSide) return;

            if (!mCollectible.HasBehavior<AnimatableAttachable>())
            {
                mCollectible.CollectibleBehaviors = mCollectible.CollectibleBehaviors.Append(new AnimatableAttachable(collectible));
                mCollectible.CollectibleBehaviors.Last().Initialize(definition["parameters"]);
                mCollectible.CollectibleBehaviors.Last().OnLoaded(api);
            }

            Shape shape = mCollectible.GetBehavior<AnimatableAttachable>().CurrentShape;
            mCollectible.GetBehavior<AnimatableAttachable>().RenderAnyway = true;

            mAnimationManager = (api.ModLoader.GetModSystem<AnimationManagerLib.AnimationManagerLibSystem>() as IAnimationManagerProvider).GetAnimationManager();

            foreach (JsonObject category in definition["categories"].AsArray())
            {
                string categoryCode = category["code"].AsString();
                mCategories.Add(categoryCode, Utils.CategoryIdFromJson(category));
            }

            Dictionary<string, AnimationMetaData> metaDataByAnimation = new();
            foreach (JsonObject metaData in definition["metadata"].AsArray())
            {
                metaDataByAnimation.Add(metaData["code"].AsString(), ParseMetaData(metaData));
            }

            foreach (JsonObject animation in definition["animations"].AsArray())
            {
                string animationCode = animation["code"].AsString();

                foreach ((string categoryName, var category) in mCategories)
                {
                    AnimationId animationId = new(category, animationCode);
                    mAnimations.Add((categoryName, animationCode), ParseAnimations(metaDataByAnimation, shape, animation["stages"].AsArray(), animationId));
                }
            }
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            if (!mClientSide) return true;

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
            Guid runId = mAnimationManager.Run(new(AnimationTargetType.HeldItemFp), mAnimations[(category, code)]);
            mApi.Logger.Notification("Player animation: {0}:{1} ({2})", category, code, runId);
            mApi.Logger.Notification("{0}", mAnimations[(category, code)].First());
            mStartedAnimations[(entityId, code, category)] = runId;
        }

        private void StopAnimation(string code, string category, long entityId)
        {
            if (mStartedAnimations.ContainsKey((entityId, code, category))) mAnimationManager.Stop(mStartedAnimations[(entityId, code, category)]);
        }

        private AnimationRequest[] ParseAnimations(Dictionary<string, AnimationMetaData> metaData, Shape shape, JsonObject[] animationsDefinitions, AnimationId id)
        {
            List<AnimationRequest> requests = new();
            foreach (JsonObject requestDefinition in animationsDefinitions)
            {
                AnimationRequest request = Utils.AnimationRequestFromJson(requestDefinition, id.Category);
                mApi.Logger.Notification("Registering: {0}", request);
                requests.Add(request);
                string animationCode = requestDefinition["animation"].AsString();
                if (!mAnimationManager.Register(request.Animation, animationCode, shape, metaData[animationCode]))
                {
                    mApi.Logger.Error("Failed to register: {0}", request);
                }
            }

            return requests.ToArray();
        }

        private AnimationMetaData ParseMetaData(JsonObject definition)
        {
            Dictionary<string, EnumAnimationBlendMode> elementBlendMode = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float> elementWeight = new();

            foreach (JsonObject element in definition["elements"].AsArray())
            {
                string code = element["code"].AsString();
                EnumAnimationBlendMode blendMode = (EnumAnimationBlendMode)Enum.Parse(typeof(EnumAnimationBlendMode), element["blendMode"].AsString());
                float weight = element["weight"].AsFloat();

                elementBlendMode.Add(code, blendMode);
                elementWeight.Add(code, weight);
            }

            AnimationMetaData metaData = new AnimationMetaData()
            {
                Animation = definition["code"].AsString(),
                Code = definition["code"].AsString(),
                ElementBlendMode = elementBlendMode,
                ElementWeight = elementWeight
            };

            return metaData;
        }
    }
}
