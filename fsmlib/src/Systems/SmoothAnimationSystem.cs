using MaltiezFSM.BullseyeCompatibility;
using System.Linq;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;
using MaltiezFSM.API;

namespace MaltiezFSM.Systems
{
    public class SmoothAnimation : BaseSystem // Based on code from TeacupAngel (https://github.com/TeacupAngel)
    {
        private bool mSystemEnabled;
        private readonly Dictionary<string, AnimationMetaData> mAnimations = new();
        private readonly Dictionary<string, Dictionary<string, string>> mAttachmentsNames = new();
        private readonly Dictionary<string, Dictionary<string, JsonObject>> mAttachmentsTransforms = new();
        private readonly Dictionary<string, Dictionary<string, IItemStackProvider>> mAttachments = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mSystemEnabled = api.ModLoader.IsModEnabled("bullseye");

            if (!mSystemEnabled) return;

            if (!mCollectible.HasBehavior<SmoothAnimationAttachableBehavior>())
            {
                mApi.Logger.Notification("[FSMlin] [SmoothAnimation] adding SmoothAnimationAttachableBehavior");
                mCollectible.CollectibleBehaviors = mCollectible.CollectibleBehaviors.Append(new SmoothAnimationAttachableBehavior(collectible));
                mCollectible.CollectibleBehaviors.Last().Initialize(definition["parameters"]);
                mCollectible.CollectibleBehaviors.Last().OnLoaded(api);
            }

            foreach (JsonObject itemProvider in definition["animations"].AsArray())
            {
                string animationCode = itemProvider["code"].AsString();
                
                mAnimations.Add(animationCode, new AnimationMetaData()
                {
                    Animation = itemProvider["parameters"]["animation"].AsString(""),
                    Code = itemProvider["parameters"]["code"].AsString(""),
                    AnimationSpeed = itemProvider["parameters"]["animationSpeed"].AsFloat(0.5f),
                    EaseOutSpeed = itemProvider["parameters"]["easeOutSpeed"].AsFloat(6),
                    EaseInSpeed = itemProvider["parameters"]["easeInSpeed"].AsFloat(16)
                });

                mAttachmentsNames.Add(animationCode, new());
                mAttachmentsTransforms.Add(animationCode, new());

                foreach (JsonObject attachment in itemProvider["attachments"].AsArray())
                {
                    mAttachmentsNames[animationCode].Add(attachment["attachment"].AsString(), attachment["system"].AsString());
                    mAttachmentsTransforms[animationCode].Add(attachment["attachment"].AsString(), attachment["transform"]);
                }
            }
        }
        public override void SetSystems(Dictionary<string, ISystem> systems)
        {
            foreach ((string code, Dictionary<string, string> systemsNames) in mAttachmentsNames)
            {
                mAttachments.Add(code, new());
                foreach ((string attachment, string system) in systemsNames)
                {
                    mAttachments[code].Add(attachment, systems[system] as IItemStackProvider);
                }
            }

            mAttachmentsNames.Clear();
        }
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;
            if (!mSystemEnabled) return true;
            SmoothAnimationAttachableBehavior animationBehavior = mCollectible.GetBehavior<SmoothAnimationAttachableBehavior>();
            if (animationBehavior == null) return true;

            string action = parameters["action"].AsString();
            string code = parameters["code"].AsString();
            switch (action)
            {
                case "start":
                    if (mApi.Side == EnumAppSide.Client)
                    {
                        animationBehavior.ClearAttachments();
                        StartAnimation(slot, player, code, animationBehavior);
                    }
                    break;
                case "stop":
                    if (mApi.Side == EnumAppSide.Client)
                    {
                        animationBehavior.StopAnimation(mAnimations[code].Code, true);
                    }
                    break;
                case "clear":
                    if (mApi.Side == EnumAppSide.Client)
                    {
                        animationBehavior.ClearAttachments();
                    }
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [SmoothAnimation] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }
        private void StartAnimation(ItemSlot slot, EntityAgent player, string code, SmoothAnimationAttachableBehavior behavior)
        {
            foreach ((string attachment, IItemStackProvider system) in mAttachments[code])
            {
                behavior.AddAttachment(attachment, system.GetItemStack(slot, player), mAttachmentsTransforms[code][attachment]);
            }

            behavior.StartAnimation(mAnimations[code]);
        }
    }
}
