using System.Linq;
using Vintagestory.API.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;
using MaltiezFSM.API;
using MaltiezFSM.Framework;
using AnimationManagerLib.CollectibleBehaviors;

namespace MaltiezFSM.Systems
{
    public class SmoothAnimation : BaseSystem // Based on code from TeacupAngel (https://github.com/TeacupAngel)
    {
        private const float mInstantSpeed = 1e4f;

        private bool mSystemEnabled;
        private string mActiveAnimationAttribute;
        private bool[] mDefaultAnimations;
        private bool mClientSide;
        private readonly Dictionary<int, string> mAnimationCodes = new();
        private readonly Dictionary<string, int> mAnimationIndices = new();
        private readonly Dictionary<string, AnimationMetaData> mAnimations = new();
        private readonly Dictionary<string, AnimationMetaData> mInstantAnimations = new();
        private readonly Dictionary<string, Dictionary<string, string>> mAttachmentsNames = new();
        private readonly Dictionary<string, Dictionary<string, JsonObject>> mAttachmentsTransforms = new();
        private readonly Dictionary<string, Dictionary<string, IItemStackProvider>> mAttachments = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mActiveAnimationAttribute = "FSMlib." + code + ".active";
            mSystemEnabled = true; // api.ModLoader.IsModEnabled("bullseye");
            mClientSide = mApi.Side == EnumAppSide.Client;

            if (!mSystemEnabled) return;

            if (!mCollectible.HasBehavior<AnimatableAttachable>())
            {
                AnimatableAttachable animationBehavior = new AnimatableAttachable(collectible);
                mCollectible.CollectibleBehaviors = mCollectible.CollectibleBehaviors.Append(animationBehavior);
                mCollectible.CollectibleBehaviors.Last().Initialize(definition["parameters"]);
                mCollectible.CollectibleBehaviors.Last().OnLoaded(api);
                animationBehavior.RenderProceduralAnimations = true;
            }

            int index = 0;
            foreach (JsonObject itemProvider in definition["animations"].AsArray())
            {
                string animationCode = itemProvider["code"].AsString();

                mAnimationCodes.Add(index, animationCode);
                mAnimationIndices.Add(animationCode, index);
                index++;
                mAnimations.Add(animationCode, new AnimationMetaData()
                {
                    Animation = itemProvider["parameters"]["animation"].AsString(""),
                    Code = itemProvider["parameters"]["code"].AsString(""),
                    AnimationSpeed = itemProvider["parameters"]["animationSpeed"].AsFloat(1),
                    EaseOutSpeed = itemProvider["parameters"]["easeOutSpeed"].AsFloat(1),
                    EaseInSpeed = itemProvider["parameters"]["easeInSpeed"].AsFloat(1)
                });

                mInstantAnimations.Add(animationCode, new AnimationMetaData()
                {
                    Animation = itemProvider["parameters"]["animation"].AsString(""),
                    Code = itemProvider["parameters"]["code"].AsString(""),
                    AnimationSpeed = mInstantSpeed,
                    EaseOutSpeed = mInstantSpeed,
                    EaseInSpeed = mInstantSpeed
                });

                mAttachmentsNames.Add(animationCode, new());
                mAttachmentsTransforms.Add(animationCode, new());

                foreach (JsonObject attachment in itemProvider["attachments"].AsArray())
                {
                    mAttachmentsNames[animationCode].Add(attachment["attachment"].AsString(), attachment["system"].AsString());
                    mAttachmentsTransforms[animationCode].Add(attachment["attachment"].AsString(), attachment["transform"]);
                }
            }

            mDefaultAnimations = new bool[index];
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
            AnimatableAttachable animationBehavior = mCollectible.GetBehavior<AnimatableAttachable>();
            if (animationBehavior == null) return true;

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    animationBehavior.ClearAttachments();
                    string codeToStart = parameters["code"].AsString("");
                    AddActiveAnimation(slot, codeToStart);
                    if (mClientSide) StartAnimation(slot, player, codeToStart, mAnimations[codeToStart], animationBehavior);
                    break;
                case "stop":
                    string codeToStop = parameters["code"].AsString("");
                    RemoveActiveAnimation(slot, codeToStop);
                    if (mClientSide) animationBehavior.StopAnimation(mAnimations[codeToStop].Code, true);
                    break;
                case "clear":
                    animationBehavior.ClearAttachments();
                    break;
                case "last":
                    animationBehavior.ClearAttachments();
                    if (mClientSide) RestoreAnimations(slot, player, animationBehavior);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [SmoothAnimation] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }
        private void StartAnimation(ItemSlot slot, EntityAgent player, string code, AnimationMetaData animation, AnimatableAttachable behavior)
        {
            if (!mClientSide) return;

            foreach ((string attachment, IItemStackProvider system) in mAttachments[code])
            {
                ItemStack stack = system.GetItemStack(slot, player);
                if (stack == null) continue;
                behavior.AddAttachment(attachment, stack, Utils.ToTransformFrom(mAttachmentsTransforms[code][attachment]));
            }

            behavior.StartAnimation(animation);
        }
        private void StopAllAnimations(AnimatableAttachable behavior)
        {
            if (!mClientSide) return;

            foreach ((string code, _) in mAnimations)
            {
                behavior.StopAnimation(mAnimations[code].Code, true);
            }
        }
        private void RestoreAnimations(ItemSlot slot, EntityAgent player, AnimatableAttachable behavior)
        {
            HashSet<string> codes = GetActiveAnimations(slot);

            StopAllAnimations(behavior);

            foreach (string code in codes)
            {
                StartAnimation(slot, player, code, mInstantAnimations[code], behavior);
            }
        }

        private void AddActiveAnimation(ItemSlot slot, string code)
        {
            bool[] animations = slot.Itemstack.Attributes.GetArray(mActiveAnimationAttribute, mDefaultAnimations);
            animations[mAnimationIndices[code]] = true;
            slot.Itemstack.Attributes.SetArray(mActiveAnimationAttribute, animations);
            slot.MarkDirty();
        }
        private void RemoveActiveAnimation(ItemSlot slot, string code)
        {
            bool[] animations = slot.Itemstack.Attributes.GetArray(mActiveAnimationAttribute, mDefaultAnimations);
            animations[mAnimationIndices[code]] = false;
            slot.Itemstack.Attributes.SetArray(mActiveAnimationAttribute, animations);
            slot.MarkDirty();
        }
        private HashSet<string> GetActiveAnimations(ItemSlot slot)
        {
            bool[] animations = slot.Itemstack.Attributes.GetArray(mActiveAnimationAttribute, mDefaultAnimations);
            HashSet<string> activeAnimations = new();
            for (int index = 0; index < animations.Length; index++)
            {
                if (animations[index]) activeAnimations.Add(mAnimationCodes[index]);
            }
            return activeAnimations;
        }
    }
}
