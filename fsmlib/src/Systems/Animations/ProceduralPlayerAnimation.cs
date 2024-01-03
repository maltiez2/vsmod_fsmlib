using AnimationManagerLib;
using AnimationManagerLib.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Systems;

public class ProceduralPlayerAnimation : BaseSystem, IAnimationSystem
{
    private readonly IAnimationManagerSystem mAnimationManager;
    private readonly Dictionary<(string category, string animation), AnimationRequest[]> mAnimations = new();
    private readonly Dictionary<string, Category> mCategories = new();
    private readonly Dictionary<(long entityId, string animation, string category), Guid> mStartedAnimations = new();

    public ProceduralPlayerAnimation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mAnimationManager = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

        if (mApi.Side != EnumAppSide.Client) return;

        Framework.Utils.Iterate(definition["categories"], (categoryCode, category) => mCategories.Add(categoryCode, Utils.CategoryFromJson(category)));
        Framework.Utils.Iterate(definition["animations"], (animationCode, animation) =>
        {
            foreach ((string categoryName, Category category) in mCategories)
            {
                ParseAnimations(animation["stages"].AsArray(), categoryName, category, animationCode);
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
        Guid runId = mAnimationManager.Run(AnimationTarget.Entity(entityId), mAnimations[(category, code)]);
        mStartedAnimations[(entityId, code, category)] = runId;
    }

    private void StopAnimation(string code, string category, long entityId)
    {
        if (mStartedAnimations.ContainsKey((entityId, code, category))) mAnimationManager.Stop(mStartedAnimations[(entityId, code, category)]);
    }

    private void ParseAnimations(JsonObject[] animationsDefinitions, string categoryName, Category category, string animation)
    {
        List<AnimationRequest> requests = new();
        foreach (JsonObject requestDefinition in animationsDefinitions)
        {
            string animationCode = requestDefinition["animation"].AsString();
            if (animationCode == null)
            {
                LogError($"No animation code provided for: {animation}");
                return;
            }
            AnimationId animationId = new(category, animationCode);
            RunParameters? parameters = Utils.RunParametersFromJson(requestDefinition, out string errorMessage);
            if (parameters == null)
            {
                LogError($"Error on parsing animations: {errorMessage}");
                return;
            }
            AnimationRequest request = new(animationId, parameters.Value);
            requests.Add(request);
            AnimationData animationData = AnimationManagerLib.API.AnimationData.Player(animationCode);
            mAnimationManager.Register(animationId, animationData);
        }

        mAnimations.Add((categoryName, animation), requests.ToArray());
    }
}
