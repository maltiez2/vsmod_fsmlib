using AnimationManagerLib.CollectibleBehaviors;
using MaltiezFSM.API;
using MaltiezFSM.Framework;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace MaltiezFSM.Systems;

public class ItemAnimation : BaseSystem // Based on code from TeacupAngel (https://github.com/TeacupAngel)
{
    private const float mInstantSpeed = 1e4f;

    private readonly string mActiveAnimationAttribute;
    private readonly bool[] mDefaultAnimations;
    private readonly bool mClientSide;
    private readonly Dictionary<int, string> mAnimationCodes = new();
    private readonly Dictionary<string, int> mAnimationIndices = new();
    private readonly Dictionary<string, AnimationMetaData> mAnimations = new();
    private readonly Dictionary<string, AnimationMetaData> mInstantAnimations = new();
    private readonly Dictionary<string, Dictionary<string, string>> mAttachmentsNames = new();
    private readonly Dictionary<string, Dictionary<string, JsonObject>> mAttachmentsTransforms = new();
    private readonly Dictionary<string, Dictionary<string, IItemStackHolder>> mAttachments = new();

    public ItemAnimation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mActiveAnimationAttribute = "FSMlib." + code + ".active";
        mClientSide = mApi.Side == EnumAppSide.Client;

        if (!mCollectible.HasBehavior<AnimatableAttachable>())
        {
            AnimatableAttachable animationBehavior = new(collectible);
            mCollectible.CollectibleBehaviors = mCollectible.CollectibleBehaviors.Append(animationBehavior);
            mCollectible.CollectibleBehaviors[^1].Initialize(definition["parameters"]);
            mCollectible.CollectibleBehaviors[^1].OnLoaded(api);
            animationBehavior.RenderProceduralAnimations = true;
        }

        int index = 0;

        Utils.Iterate(definition["animations"], (animationCode, animationDefinition) =>
        {
            mAnimationCodes.Add(index, animationCode);
            mAnimationIndices.Add(animationCode, index);
            index++;
            mAnimations.Add(animationCode, new AnimationMetaData()
            {
                Animation = animationDefinition["parameters"]["animation"].AsString(""),
                Code = animationDefinition["parameters"]["code"].AsString(""),
                AnimationSpeed = animationDefinition["parameters"]["animationSpeed"].AsFloat(1),
                EaseOutSpeed = animationDefinition["parameters"]["easeOutSpeed"].AsFloat(1),
                EaseInSpeed = animationDefinition["parameters"]["easeInSpeed"].AsFloat(1)
            });

            mInstantAnimations.Add(animationCode, new AnimationMetaData()
            {
                Animation = animationDefinition["parameters"]["animation"].AsString(""),
                Code = animationDefinition["parameters"]["code"].AsString(""),
                AnimationSpeed = mInstantSpeed,
                EaseOutSpeed = mInstantSpeed,
                EaseInSpeed = mInstantSpeed
            });

            mAttachmentsNames.Add(animationCode, new());
            mAttachmentsTransforms.Add(animationCode, new());

            foreach (JsonObject attachment in animationDefinition["attachments"].AsArray())
            {
                mAttachmentsNames[animationCode].Add(attachment["attachment"].AsString(), attachment["system"].AsString());
                mAttachmentsTransforms[animationCode].Add(attachment["attachment"].AsString(), attachment["transform"]);
            }
        });

        mDefaultAnimations = new bool[index];
    }
    public override void SetSystems(Dictionary<string, ISystem> systems)
    {
        foreach ((string code, Dictionary<string, string> systemsNames) in mAttachmentsNames)
        {
            mAttachments.Add(code, new());
            foreach ((string attachment, string system) in systemsNames)
            {
                IItemStackHolder? systemInstance = GetSystem(systems, system);
                if (systemInstance == null) continue;
                mAttachments[code].Add(attachment, systemInstance);
            }
        }

        mAttachmentsNames.Clear();
    }
    private IItemStackHolder? GetSystem(Dictionary<string, ISystem> systems, string system)
    {
        if (!systems.ContainsKey(system) || systems[system] is not IItemStackHolder)
        {
            IEnumerable<string> soundSystems = systems.Where((entry, _) => entry.Value is IItemStackHolder).Select((entry, _) => entry.Key);
            LogError($"ItemStack holder system '{system}' not found. Available systems: {Utils.PrintList(soundSystems)}.");
            return null;
        }

        return systems[system] as IItemStackHolder;
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;
        AnimatableAttachable animationBehavior = mCollectible.GetBehavior<AnimatableAttachable>();
        if (animationBehavior == null) return true;

        string action = parameters["action"].AsString("start");
        switch (action)
        {
            case "start":
                animationBehavior.ClearAttachments(player.Entity.EntityId);
                string? codeToStart = parameters["animation"].AsString();
                if (!CheckAnimationCode(codeToStart)) return true;
                AddActiveAnimation(slot, codeToStart);
                if (mClientSide) StartAnimation(slot, player, codeToStart, mAnimations[codeToStart], animationBehavior);
                break;
            case "stop":
                string? codeToStop = parameters["animation"].AsString("");
                if (!CheckAnimationCode(codeToStop)) return true;
                RemoveActiveAnimation(slot, codeToStop);
                if (mClientSide) animationBehavior.StopAnimation(mAnimations[codeToStop].Code, player.Entity, true);
                break;
            case "clear":
                animationBehavior.ClearAttachments(player.Entity.EntityId);
                break;
            case "last":
                animationBehavior.ClearAttachments(player.Entity.EntityId);
                if (mClientSide) RestoreAnimations(slot, player, animationBehavior);
                break;
            default:
                LogActions(action, "start", "stop", "clear", "last");
                return false;
        }
        return true;
    }
    private bool CheckAnimationCode(string? code)
    {
        if (code == null)
        {
            LogError("No 'animation' in system request");
            return false;
        }

        if (!mAnimations.ContainsKey(code))
        {
            LogError($"Animation '{code}' was not found");
            return false;
        }

        return true;
    }
    private void StartAnimation(ItemSlot slot, IPlayer player, string code, AnimationMetaData animation, AnimatableAttachable behavior)
    {
        if (!mClientSide) return;

        foreach ((string attachment, IItemStackHolder system) in mAttachments[code])
        {
            IEnumerable<ItemSlot> stacks = system.Get(slot, player);
            if (!stacks.Any()) continue;

            behavior.SetAttachment(player.Entity.EntityId, attachment, stacks.First().Itemstack, Utils.ToTransformFrom(mAttachmentsTransforms[code][attachment]) ?? new());
        }

        behavior.StartAnimation(animation, player.Entity);
    }
    private void StopAllAnimations(AnimatableAttachable behavior, Entity entity)
    {
        if (!mClientSide) return;

        foreach ((string code, _) in mAnimations)
        {
            behavior.StopAnimation(mAnimations[code].Code, entity, true);
        }
    }
    private void RestoreAnimations(ItemSlot slot, IPlayer player, AnimatableAttachable behavior)
    {
        HashSet<string> codes = GetActiveAnimations(slot);

        StopAllAnimations(behavior, player.Entity);

        foreach (string code in codes)
        {
            StartAnimation(slot, player, code, mInstantAnimations[code], behavior);
        }
    }

    private void AddActiveAnimation(ItemSlot slot, string code)
    {
        bool[]? animations = slot.Itemstack.Attributes.GetArray(mActiveAnimationAttribute, mDefaultAnimations);
        if (animations == null) return;
        animations[mAnimationIndices[code]] = true;
        slot.Itemstack.Attributes.SetArray(mActiveAnimationAttribute, animations);
        slot.MarkDirty();
    }
    private void RemoveActiveAnimation(ItemSlot slot, string code)
    {
        bool[]? animations = slot.Itemstack.Attributes.GetArray(mActiveAnimationAttribute, mDefaultAnimations);
        if (animations == null) return;
        animations[mAnimationIndices[code]] = false;
        slot.Itemstack.Attributes.SetArray(mActiveAnimationAttribute, animations);
        slot.MarkDirty();
    }
    private HashSet<string> GetActiveAnimations(ItemSlot slot)
    {
        bool[]? animations = slot.Itemstack.Attributes.GetArray(mActiveAnimationAttribute, mDefaultAnimations);
        HashSet<string> activeAnimations = new();
        if (animations == null) return activeAnimations;
        for (int index = 0; index < animations.Length; index++)
        {
            if (animations[index]) activeAnimations.Add(mAnimationCodes[index]);
        }
        return activeAnimations;
    }
}
