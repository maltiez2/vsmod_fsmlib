using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Systems;

public abstract class BaseSystem : FactoryProduct, ISystem
{
    protected BaseSystem(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    protected void LogActions(string action, params string[] actions) => Logger.Error(mApi, this, $"(system: {mCode}) Wrong action '{action}'. Available actions: {Utils.PrintList(actions)}");

    public virtual string[]? GetDescription(ItemSlot slot, IWorldAccessor world)
    {
        if (slot == null)
        {
            LogDebug($"Slot is null");
        }

        return System.Array.Empty<string>();
    }

    public virtual bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        return ValidateParameters(slot, player, parameters);
    }

    public virtual void SetSystems(Dictionary<string, ISystem> systems)
    {

    }

    public virtual bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        return ValidateParameters(slot, player, parameters);
    }

    protected bool ValidateParameters(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (slot == null)
        {
            LogError($"Slot is null");
            return false;
        }

        if (player == null)
        {
            LogWarn($"Player is null");
            return false;
        }

        if (parameters == null)
        {
            LogWarn($"Parameters are null");
            return false;
        }

        return true;
    }
}
