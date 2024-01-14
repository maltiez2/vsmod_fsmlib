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
    private readonly Dictionary<(string category, string animation), ProceduralEntityAnimationData> mAnimations = new();
    private readonly Dictionary<string, Category> mCategories = new();
    private readonly Dictionary<(long entityId, string category), ProceduralEntityAnimationId> mStartedAnimations = new();

    public ProceduralPlayerAnimation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mAnimationManager = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

        if (mApi.Side != EnumAppSide.Client) return;

        Framework.Utils.Iterate(definition["categories"], (categoryCode, category) => mCategories.Add(categoryCode, CategoryFromJson(categoryCode, category)));
        Framework.Utils.Iterate(definition["animations"], (animationCode, animation) =>
        {
            foreach ((string categoryName, Category category) in mCategories)
            {
                try
                {
                    mAnimations.Add((categoryName, animationCode), new(mAnimationManager, LogError, animation.AsArray(), category, animationCode));
                }
                catch (Exception exception)
                {
                    LogError($"Error on registering animation '{categoryName}:{animationCode}'.");
                    LogVerbose($"Error on registering animation '{categoryName}:{animationCode}'.\n\nException:\n{exception}");
                }
            }
        });
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;
        if (mApi.Side != EnumAppSide.Client) return true;


        string action = parameters["action"].AsString("start");



        switch (action)
        {
            case "start":
                string? code = parameters["animation"].AsString();
                string? category = parameters["category"].AsString();
                if (!Check(code, category)) return false;
                PlayAnimation(code, category, player.Entity.EntityId);
                break;
            case "stop":
                string? category_2 = parameters["category"].AsString();
                if (category_2 == null)
                {
                    LogError("No 'category' in system request");
                    return false;
                }
                StopAnimation(category_2, player.Entity.EntityId);
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
                StopAnimation(category, player.Entity.EntityId);
                break;
            default:
                LogActions(action, "start", "stop");
                break;
        }
    }

    private static Category CategoryFromJson(string code, JsonObject definition)
    {
        EnumAnimationBlendMode blending = (EnumAnimationBlendMode)Enum.Parse(typeof(EnumAnimationBlendMode), definition["blending"].AsString("Add"));
        float? weight = definition.KeyExists("weight") ? definition["weight"].AsFloat() : null;

        return new Category(code, blending, weight);
    }
    private bool Check(string? code, string? category)
    {
        if (code == null)
        {
            LogError("No 'animation' in system request");
            return false;
        }

        if (category == null)
        {
            LogError("No 'category' in system request");
            return false;
        }

        if (!mCategories.ContainsKey(category))
        {
            LogError($"Category '{category}' not found");
            return false;
        }

        if (!mAnimations.ContainsKey((category, code)))
        {
            LogError($"Animation '{code}' not found");
            return false;
        }

        return true;
    }
    private void PlayAnimation(string code, string category, long entityId)
    {
        ProceduralEntityAnimationId? runId = mAnimations[(category, code)].Start(mAnimationManager, entityId, (message) => LogDebug($"Animation: '{category}:{code}'. {message}"));
        if (runId == null)
        {
            LogError($"Error on running '{category}:{code}' animation.");
            return;
        }
        mStartedAnimations[(entityId, category)] = runId.Value;
    }
    private void StopAnimation(string category, long entityId)
    {
        if (mStartedAnimations.ContainsKey((entityId, category))) mStartedAnimations[(entityId, category)].Stop(mAnimationManager);
    }
}

internal class ProceduralEntityAnimationData
{
    public List<AnimationRequest> RequestsTp { get; set; } = new();
    public List<AnimationRequest> RequestsFp { get; set; } = new();
    public List<AnimationRequest> RequestsIfp { get; set; } = new();

    public ProceduralEntityAnimationData(IAnimationManagerSystem animationManager, Action<string> logger, JsonObject[] animationsDefinitions, Category category, string animation)
    {
        foreach (JsonObject requestDefinition in animationsDefinitions)
        {
            string animationCode = requestDefinition["animation"].AsString();
            if (animationCode == null)
            {
                logger($"No animation code provided for: {animation}");
                return;
            }

            string codeTp = animationCode;
            string codeFp = $"{animationCode}-fp";
            string codeIfp = $"{animationCode}-ifp";

            ConstructRequest(codeTp, RequestsTp, requestDefinition, animationManager, logger, category);
            ConstructRequest(codeFp, RequestsFp, requestDefinition, animationManager, logger, category);
            ConstructRequest(codeIfp, RequestsIfp, requestDefinition, animationManager, logger, category);
        }
    }

    private static void ConstructRequest(string animationCode, List<AnimationRequest> requests, JsonObject requestDefinition, IAnimationManagerSystem animationManager, Action<string> logger, Category category)
    {
        AnimationId animationId = new(category, animationCode);
        RunParameters? parameters = Utils.RunParametersFromJson(requestDefinition, out string errorMessage);
        if (parameters == null)
        {
            logger($"Error on parsing animations: {errorMessage}");
            return;
        }
        AnimationRequest request = new(animationId, parameters.Value);
        AnimationData animationData = AnimationData.Player(animationCode);
        animationManager.Register(animationId, animationData);
        requests.Add(request);
    }

    public ProceduralEntityAnimationId? Start(IAnimationManagerSystem animationManager, long entityId, Action<string> logger)
    {
        System.Guid tpRun;
        System.Guid fpRun;
        System.Guid ifpRun;

        try
        {
            tpRun = animationManager.Run(AnimationTarget.Entity(entityId, AnimationTargetType.EntityThirdPerson), RequestsTp.ToArray());
        }
        catch
        {
            return null;
        }

        try
        {
            fpRun = animationManager.Run(AnimationTarget.Entity(entityId, AnimationTargetType.EntityFirstPerson), RequestsFp.ToArray());
        }
        catch
        {
            fpRun = animationManager.Run(AnimationTarget.Entity(entityId, AnimationTargetType.EntityFirstPerson), RequestsTp.ToArray());
            logger($"Error on running fp animation, using tp version instead.");
            RequestsFp = RequestsTp;
        }

        try
        {
            ifpRun = animationManager.Run(AnimationTarget.Entity(entityId, AnimationTargetType.EntityImmersiveFirstPerson), RequestsIfp.ToArray());
        }
        catch
        {
            ifpRun = animationManager.Run(AnimationTarget.Entity(entityId, AnimationTargetType.EntityImmersiveFirstPerson), RequestsTp.ToArray());
            logger($"Error on running ifp animation, using tp version instead");
            RequestsIfp = RequestsTp;
        }

        return new(
            tpRun,
            fpRun,
            ifpRun
        );
    }
}

internal readonly struct ProceduralEntityAnimationId
{
    public System.Guid IdTp { get; }
    public System.Guid IdFp { get; }
    public System.Guid IdIfp { get; }

    public ProceduralEntityAnimationId(System.Guid idTp, System.Guid idFp, System.Guid idIfp)
    {
        IdTp = idTp;
        IdFp = idFp;
        IdIfp = idIfp;
    }

    public readonly void Stop(IAnimationManagerSystem animationManager)
    {
        animationManager.Stop(IdTp);
        animationManager.Stop(IdFp);
        animationManager.Stop(IdIfp);
    }
}
