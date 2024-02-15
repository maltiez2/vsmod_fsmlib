using AnimationManagerLib.CollectibleBehaviors;
using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class Attachments : BaseSystem
{
    private readonly AnimatableAttachable? mBehavior;
    private readonly string mStackProviderSystem;
    private IItemStackHolder? mStackProvider;
    private readonly Dictionary<string, ModelTransform> mTransforms = new();

    public Attachments(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mStackProviderSystem = definition["stackSystem"].AsString();

        if (!mCollectible.HasBehavior<AnimatableProcedural>(withInheritance: true))
        {
            LogError($"'Animatable' behavior was not found");
            return;
        }

        mBehavior = mCollectible.GetBehavior<AnimatableProcedural>();

        Utils.Iterate(definition["transforms"], (code, definition) => mTransforms.Add(code, Utils.ToTransformFrom(definition) ?? ModelTransform.ItemDefaultTp()));
    }

    public override void SetSystems(Dictionary<string, ISystem> systems)
    {
        if (!systems.ContainsKey(mStackProviderSystem))
        {
            LogError($"System '{mStackProviderSystem}' not found");
            return;
        }

        if (systems[mStackProviderSystem] is not IItemStackHolder system)
        {
            LogError($"System '{systems[mStackProviderSystem]}' is not 'IItemStackHolder'");
            return;
        }

        mStackProvider = system;
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        if (mStackProvider == null || mBehavior == null) return true;

        string action = parameters["action"].AsString("add");

        if (!parameters.KeyExists("point"))
        {
            LogError($"Request does not contain 'point' field");
            return true;
        }

        string attachmentPoint = parameters["point"].AsString();
        

        switch (action)
        {
            case "add":
                Add(attachmentPoint, slot, player, parameters);
                break;
            case "remove":
                mBehavior.ToggleAttachment(player.Entity.EntityId, attachmentPoint, false);
                break;
            case "refresh":
                if (mStackProvider.Get(slot, player).Count == 0)
                {
                    mBehavior.ToggleAttachment(player.Entity.EntityId, attachmentPoint, false);
                    return true;
                }
                if (mBehavior.CheckAttachment(player.Entity.EntityId, attachmentPoint) == null)
                {
                    Add(attachmentPoint, slot, player, parameters);
                    return true;
                }
                if (mBehavior.CheckAttachment(player.Entity.EntityId, attachmentPoint) == true)
                {
                    mBehavior.ToggleAttachment(player.Entity.EntityId, attachmentPoint, true);
                    return true;
                }
                break;

            default:
                LogActions(action, "add", "remove", "refresh");
                return false;
        }

        return true;
    }

    public void Add(string attachmentPoint, ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!parameters.KeyExists("transform"))
        {
            LogError($"Request does not contain 'transform' field");
            return;
        }
        string transform = parameters["transform"].AsString();
        List<ItemStack> stacks = mStackProvider?.Get(slot, player) ?? new();
        if (stacks.Count == 0) return;
        mBehavior?.SetAttachment(player.Entity.EntityId, attachmentPoint, stacks[0], mTransforms[transform], true);
    }
}
