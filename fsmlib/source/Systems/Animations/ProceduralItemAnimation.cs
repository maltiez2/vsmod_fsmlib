using AnimationManagerLib;
using AnimationManagerLib.API;
using AnimationManagerLib.CollectibleBehaviors;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class ProceduralItemAnimation : BaseSystem, IAnimationSystem
{
    private readonly IAnimationManagerSystem mAnimationManager;
    private readonly Dictionary<(string category, string animation), ProceduralItemAnimationData> mAnimations = new();
    private readonly Dictionary<string, Category> mCategories = new();
    private readonly Dictionary<(long entityId, string category), ProceduralItemAnimationId> mStartedAnimations = new();

    public ProceduralItemAnimation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mAnimationManager = api.ModLoader.GetModSystem<AnimationManagerLibSystem>();

        if (mApi.Side != EnumAppSide.Client) return;

        if (!mCollectible.HasBehavior<AnimatableProcedural>(withInheritance: true))
        {
            LogError($"'Animatable' behavior was not found");
            return;
        }

        AnimatableProcedural behavior = mCollectible.GetBehavior<AnimatableProcedural>();

        Framework.Utils.Iterate(definition["categories"], (categoryCode, category) => mCategories.Add(categoryCode, CategoryFromJson(categoryCode, category)));
        Framework.Utils.Iterate(definition["animations"], (sequenceCode, animation) =>
        {
            foreach ((string categoryName, Category category) in mCategories)
            {
                try
                {
                    mAnimations.Add((categoryName, sequenceCode), new(behavior, mAnimationManager, LogError, animation.AsArray(), category, sequenceCode, GetAnimationCode));
                }
                catch (Exception exception)
                {
                    LogError($"Error on registering animation '{categoryName}:{sequenceCode}'.");
                    LogVerbose($"Error on registering animation '{categoryName}:{sequenceCode}'.\n\nException:\n{exception}");
                }
            }
        });
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("start");

        switch (action)
        {
            case "start":
                string? code = parameters["animation"].AsString();
                string? category = parameters["category"].AsString();
                if (!Check(code, category) || code == null) return true;
                if (mApi.Side != EnumAppSide.Client) return true;
                PlayAnimation(code, category, player.Entity.EntityId);
                break;
            case "stop":
                string? category_2 = parameters["category"].AsString();
                if (category_2 == null)
                {
                    LogError("No 'category' in system request");
                    return false;
                }
                if (mApi.Side != EnumAppSide.Client) return true;
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
            if (mApi.Side == EnumAppSide.Client) LogError($"Category '{category}' not found");
            return false;
        }

        if (!mAnimations.ContainsKey((category, code)))
        {
            if (mApi.Side == EnumAppSide.Client) LogError($"Animation '{code}' not found");
            return false;
        }

        return true;
    }
    private void PlayAnimation(string code, string category, long entityId)
    {
        mStartedAnimations[(entityId, category)] = mAnimations[(category, code)].Start(mAnimationManager, entityId);
    }
    private void StopAnimation(string category, long entityId)
    {
        if (mStartedAnimations.ContainsKey((entityId, category))) mStartedAnimations[(entityId, category)].Stop(mAnimationManager);
    }
    private string? GetAnimationCode(string? code) => code == null ? code : $"{mCollectible.Code}|{mCode}|{code}";
}

internal class ProceduralItemAnimationData
{
    private AnimationSequence? mThirdPersonSequence;
    private AnimationSequence? mFirstPersonSequence;
    private AnimationTarget mThirdPersonTarget;
    private AnimationTarget mFirstPersonTarget;

    public ProceduralItemAnimationData(AnimatableProcedural behavior, IAnimationManagerSystem animationManager, Action<string> logger, JsonObject[] animationsDefinitions, Category category, string sequenceCode, System.Func<string?, string?> constructCode)
    {
        List<AnimationRequest> tpRequests = new();
        List<AnimationRequest> fpRequests = new();

        Shape? fpShape = behavior.FirstPersonShape;
        Shape? tpShape = behavior.ThirdPersonShape;


        foreach (JsonObject requestDefinition in animationsDefinitions)
        {
            string animationCode = requestDefinition["animation"].AsString();
            if (animationCode == null)
            {
                logger($"No animation code provided for: {sequenceCode}");
                return;
            }

            string fpCode = $"{constructCode.Invoke(animationCode)}-fp";
            string tpCode = $"{constructCode.Invoke(animationCode)}-tp";

            string codeTp = animationCode;
            string codeFp = animationCode;

            bool separateFp = requestDefinition["separateFp"].AsBool(false);
            if (separateFp) codeFp = $"{animationCode}-fp";

            if (fpShape != null)
            {
                ConstructRequest(fpShape, fpCode, codeTp, fpRequests, requestDefinition, animationManager, logger, category);
            }

            if (tpShape != null)
            {
                ConstructRequest(tpShape, tpCode, codeFp, tpRequests, requestDefinition, animationManager, logger, category);
            }
        }

        if (tpRequests.Count > 0) mThirdPersonSequence = new(tpRequests.ToArray());
        if (fpRequests.Count > 0) mFirstPersonSequence = new(fpRequests.ToArray());
    }

    private static void ConstructRequest(Shape shape, string animationName, string animationCode, List<AnimationRequest> requests, JsonObject requestDefinition, IAnimationManagerSystem animationManager, Action<string> logger, Category category)
    {
        AnimationId animationId = new(category, animationName);
        RunParameters? parameters = Utils.RunParametersFromJson(requestDefinition, out string errorMessage);
        if (parameters == null)
        {
            logger($"Error on parsing animations: {errorMessage}");
            return;
        }
        AnimationRequest request = new(animationId, parameters.Value);

        AnimationData animationData = AnimationData.HeldItem(animationCode, shape);
        animationManager.Register(animationId, animationData);
        requests.Add(request);
    }

    public ProceduralItemAnimationId Start(IAnimationManagerSystem animationManager, long entityId)
    {
        mThirdPersonTarget = new AnimationTarget(entityId, AnimationTargetType.HeldItemTp);
        mFirstPersonTarget = new AnimationTarget(entityId, AnimationTargetType.HeldItemFp);

        Guid? tpRun = (mThirdPersonSequence != null) ? animationManager.Run(mThirdPersonTarget, mThirdPersonSequence.Value) : null;
        Guid? fpRun = (mFirstPersonSequence != null) ? animationManager.Run(mFirstPersonTarget, mFirstPersonSequence.Value) : null;

        return new(tpRun, fpRun);
    }
}

internal readonly struct ProceduralItemAnimationId
{
    private readonly Guid? IdTp;
    private readonly Guid? IdFp;

    public ProceduralItemAnimationId(Guid? idTp, Guid? idFp)
    {
        IdTp = idTp;
        IdFp = idFp;
    }

    public readonly void Stop(IAnimationManagerSystem animationManager)
    {
        if (IdTp != null) animationManager.Stop(IdTp.Value);
        if (IdFp != null) animationManager.Stop(IdFp.Value);
    }
}