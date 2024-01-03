using AnimationManagerLib;
using AnimationManagerLib.API;
using AnimationManagerLib.CollectibleBehaviors;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Systems;

public class ProceduralItemAnimation : BaseSystem, IAnimationSystem
{
    private readonly IAnimationManagerSystem? mAnimationManager;
    private readonly Dictionary<(string category, string animation), AnimationRequest[]> mAnimations = new();
    private readonly Dictionary<string, Category> mCategories = new();
    private readonly Dictionary<(long entityId, string animation, string category), Guid> mStartedAnimations = new();
    private readonly Animatable? mBehavior;

    public ProceduralItemAnimation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        if (mApi.Side != EnumAppSide.Client) return;

        mAnimationManager = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

        if (!mCollectible.HasBehavior<Animatable>(withInheritance: true))
        {
            LogError($"'Animatable' behavior was not found");
            return;
        }

        mBehavior = mCollectible.GetBehavior<Animatable>();
        mBehavior.RenderProceduralAnimations = true;

        Framework.Utils.Iterate(definition["categories"], (categoryCode, category) => mCategories.Add(categoryCode, Utils.CategoryFromJson(category)));
        Framework.Utils.Iterate(definition["animations"], (animationCode, animation) =>
        {
            foreach ((string categoryName, _) in mCategories)
            {
                ParseAnimations(animation["stages"].AsArray(), categoryName, animationCode);
            }
        });
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;
        if (mApi.Side != EnumAppSide.Client) return true;

        string code = parameters["animation"].AsString();
        string category = parameters["category"].AsString();
        string action = parameters["action"].AsString();
        switch (action)
        {
            case "start":
                PlayAnimation(code, category, player.Entity.EntityId);
                break;
            case "stop":
                StopAnimation(code, category, player.Entity.EntityId);
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }

        return true;
    }

    public void PlayAnimation(ItemSlot slot, IPlayer player, string code, string category, string action = "start", float durationMultiplier = 1)
    {
        switch (action)
        {
            case "start":
                PlayAnimation(code, category, player.Entity.EntityId);
                break;
            case "stop":
                StopAnimation(code, category, player.Entity.EntityId);
                break;
            default:
                LogActions(action, "start", "stop");
                break;
        }
    }

    private void PlayAnimation(string code, string category, long entityId)
    {
        Guid? runId = mAnimationManager?.Run(AnimationTarget.HeldItem(), mAnimations[(category, code)]);
        if (runId != null) mStartedAnimations[(entityId, code, category)] = runId.Value;
    }

    private void StopAnimation(string code, string category, long entityId)
    {
        if (mStartedAnimations.ContainsKey((entityId, code, category))) mAnimationManager?.Stop(mStartedAnimations[(entityId, code, category)]);
    }

    private void ParseAnimations(JsonObject[] animationsDefinitions, string category, string animation)
    {
        if (mBehavior?.CurrentShape == null || mAnimationManager == null) return;

        List<AnimationRequest> requests = new();
        foreach (JsonObject requestDefinition in animationsDefinitions)
        {
            string animationCode = requestDefinition["code"].AsString();
            AnimationId animationId = new(category, animationCode);
            RunParameters? parameters = Utils.RunParametersFromJson(requestDefinition, out string errorMessage);
            if (parameters == null)
            {
                LogError($"Error on parsing animations: {errorMessage}");
                return;
            }
            AnimationRequest request = new(animationId, parameters.Value);
            requests.Add(request);
            AnimationData animationData = AnimationData.HeldItem(animationCode, mBehavior.CurrentShape);
            mAnimationManager.Register(animationId, animationData);
        }

        mAnimations.Add((category, animation), requests.ToArray());
    }
}
