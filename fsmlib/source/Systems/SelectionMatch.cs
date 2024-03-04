using MaltiezFSM.Framework;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class SelectionMatch : BaseSystem
{
    private readonly Dictionary<string, AssetLocation[]> mWildcards = new();

    public SelectionMatch(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        Utils.Iterate(definition, (wildcardCode, wildcardData) =>
        {
            if (wildcardCode == "code") return;

            List<JsonObject> wildcards = ParseField(wildcardData);

            mWildcards.Add(wildcardCode, wildcards.Select(value => new AssetLocation(value.AsString())).ToArray());
        });
    }

    public override bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Verify(slot, player, parameters)) return false;

        string wildcard = parameters["match"].AsString();

        if (!mWildcards.ContainsKey(wildcard))
        {
            LogError($"Wildcard with code '{wildcard}' not found");
            return false;
        }

        bool inverse = parameters["inverse"].AsBool(false);
        bool block = player.CurrentBlockSelection?.Block?.WildCardMatch(mWildcards[wildcard]) ?? false;
        bool entity = player.CurrentEntitySelection?.Entity?.WildCardMatch(mWildcards[wildcard]) ?? false;
        bool matched = block || entity;
        return inverse ? !matched : matched;
    }
}
