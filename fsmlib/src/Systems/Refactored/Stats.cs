using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable enable

namespace MaltiezFSM.Systems;

internal class Stats : BaseSystem
{
    private const int cDefaultTimeout = 30 * 1000;
    private const float cDefaultValue = 0;
    private readonly string mStatCode;
    private readonly Dictionary<string, TimeoutCallback> mStats = new();

    public Stats(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mStatCode = definition["code"].AsString($"fsmlib.{id}.{code}");
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string? code = parameters["code"].AsString();

        if (code == null)
        {
            Utils.Logger.Error(mApi, this, $"System '{mCode}'.No stat code was specified.");
            return false;
        }

        float value = parameters["value"].AsFloat(0);
        bool persistent = parameters["persist"].AsBool(false);
        int timeout = parameters["timeout"].AsInt(0);
        float defaultValue = cDefaultValue;
        if (parameters.KeyExists("default"))
        {
            defaultValue = parameters["default"].AsFloat(0);
        }

        if (mStats.ContainsKey(code))
        {
            mStats[code].Stop();
        }

        player.Entity.Stats.Set(code, mStatCode, value, persistent);
        mStats.TryAdd(code, new(mApi));
        mStats[code].Start(timeout > 0 ? timeout : cDefaultTimeout, (float time) => Revert(time, player, code, defaultValue));

        return true;
    }

    void Revert(float time, IPlayer player, string category, float defaultValue)
    {
        Utils.Logger.Verbose(mApi, this, $"System '{mCode}'. Timeout triggered for '{category}' after {time} seconds.");
        player.Entity.Stats.Set(category, mStatCode, defaultValue, true);
        mStats.Remove(category);
    }
}

public sealed class TimeoutCallback
{
    private readonly ICoreAPI mApi;
    private long mCallbackId;

    public TimeoutCallback(ICoreAPI api) => mApi = api;
    public void Start(int delay_ms, Action<float> callback) => mCallbackId = mApi.World.RegisterCallback(callback, delay_ms);
    public void Stop() => mApi.World.UnregisterCallback(mCallbackId);
}
