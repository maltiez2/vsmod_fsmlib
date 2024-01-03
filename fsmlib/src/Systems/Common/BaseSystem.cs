using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable enable

namespace MaltiezFSM.Systems;

public abstract class BaseSystem : FactoryProduct, ISystem
{
    protected BaseSystem(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    protected void LogError(string message) => Utils.Logger.Error(mApi, this, $"[system: {mCode}] {message}");
    protected void LogWarn(string message) => Utils.Logger.Warn(mApi, this, $"[system: {mCode}] {message}");
    protected void LogNotify(string message) => Utils.Logger.Notify(mApi, this, $"[system: {mCode}] {message}");
    protected void LogDebug(string message) => Utils.Logger.Debug(mApi, this, $"[system: {mCode}] {message}");
    protected void LogVerbose(string message) => Utils.Logger.Verbose(mApi, this, $"[system: {mCode}] {message}");
    protected void LogActions(string action, params string[] actions) => Utils.Logger.Error(mApi, this, $"[system: {mCode}] Wrong action '{action}'. Available actions: {Utils.PrintList(actions)}");

    virtual public string[]? GetDescription(ItemSlot slot, IWorldAccessor world)
    {
        if (slot == null)
        {
            Utils.Logger.Warn(mApi, this, $" [GetDescription] (system: {mCode}) Slot is null");
        }

        return System.Array.Empty<string>();
    }

    virtual public bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (slot  == null)
        {
            Utils.Logger.Error(mApi, this, $"[Process()] (system: {mCode}) slot is null");
            return false;
        }

        if (player == null)
        {
            Utils.Logger.Warn(mApi, this, $"[Process()] (system: {mCode}) player is null");
            return false;
        }

        if (parameters == null)
        {
            Utils.Logger.Warn(mApi, this, $"[Process()] (system: {mCode}) parameters are null");
            return false;
        }

        return true;
    }

    virtual public void SetSystems(Dictionary<string, ISystem> systems)
    {
        
    }

    virtual public bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (slot == null)
        {
            Utils.Logger.Error(mApi, this, $"[Verify()] (system: {mCode}) slot is null");
            return false;
        }

        if (player == null)
        {
            Utils.Logger.Warn(mApi, this, $"[Verify()] (system: {mCode}) player is null");
            return false;
        }

        if (parameters == null)
        {
            Utils.Logger.Warn(mApi, this, $"[Verify()] (system: {mCode}) parameters are null");
            return false;
        }

        return true;
    }
}
