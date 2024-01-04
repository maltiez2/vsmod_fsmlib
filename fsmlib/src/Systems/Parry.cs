using MaltiezFSM.Additional;
using MaltiezFSM.API;
using MaltiezFSM.Framework;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;



namespace MaltiezFSM.Systems;

public class Parry<TResistBehavior> : BaseSystem
    where TResistBehavior : EntityBehavior, ITempResistEntityBehavior
{
    private readonly Dictionary<long, IResist> mResists = new();
    private readonly Dictionary<long, long> mCallbacks = new();
    private readonly Dictionary<string, ParryData> mParries = new();
    private readonly Dictionary<string, string> mSounds = new();
    private readonly string? mSoundSystemName;
    private ISoundSystem? mSoundSystem;

    public Parry(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mSoundSystemName = definition["soundSystem"].AsString();

        if (definition.Token is not JObject parries) return;

        foreach ((string parryCode, JToken? parry) in parries)
        {
            if (parry == null) continue;
            JsonObject parryObject = new(parry);
            mParries.Add(parryCode, new(parryObject));
            mSounds.Add(parryCode, parryObject["sound"].AsString());
        }
    }

    public override void SetSystems(Dictionary<string, ISystem> systems)
    {
        if (mSoundSystemName != null)
        {
            if (!systems.ContainsKey(mSoundSystemName) || systems[mSoundSystemName] is not ISoundSystem)
            {
                IEnumerable<string> soundSystems = systems.Where((entry, _) => entry.Value is ISoundSystem).Select((entry, _) => entry.Key);
                LogError($"Sound system '{mSoundSystemName}' not found. Available sound systems: {Utils.PrintList(soundSystems)}.");
                return;
            }

            mSoundSystem = systems[mSoundSystemName] as ISoundSystem;
        }
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("start");
        switch (action)
        {
            case "start":
                if (mApi.Side != EnumAppSide.Server) return true;
                string code = parameters["parry"].AsString();
                ScheduleParry(player, code);
                break;
            case "stop":
                if (mApi.Side != EnumAppSide.Server) return true;
                StopParry(player);
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }
        return true;
    }

    private void ScheduleParry(IPlayer player, string code)
    {
        long entityId = player.Entity.EntityId;
        if (mCallbacks.ContainsKey(entityId)) mApi.World.UnregisterCallback(mCallbacks[entityId]);
        mCallbacks[entityId] = mApi.World.RegisterCallback(_ => StartParry(player, code), mParries[code].windowStart);
    }

    private void StartParry(IPlayer player, string code)
    {
        if (mResists.ContainsKey(player.Entity.EntityId)) StopParry(player);
        ITempResistEntityBehavior behavior = player.Entity.GetBehavior<TResistBehavior>();
        if (behavior == null)
        {
            LogError($"No IResistEntityBehavior found at {player.PlayerName} ({player.Entity.Class}:{player.Entity.Code})");
            return;
        }
        IResist resist = mParries[code].AddToBehavior(behavior);
        if (mSounds[code] != null) resist.SetResistCallback((IResist resist, DamageSource damageSource, ref float damage, Entity receiver) => PlayParrySound(receiver, mSounds[code]));
        mResists.Add(player.Entity.EntityId, resist);
    }

    private void StopParry(IPlayer player)
    {
        long entityId = player.Entity.EntityId;
        if (mCallbacks.ContainsKey(entityId))
        {
            mApi.World.UnregisterCallback(mCallbacks[entityId]);
            mCallbacks.Remove(entityId);
        }
        if (!mResists.ContainsKey(entityId)) return;
        IResistEntityBehavior behavior = player.Entity.GetBehavior<TResistBehavior>();
        behavior.RemoveResist(mResists[entityId]);
        mResists.Remove(entityId);
    }

    private void PlayParrySound(Entity receiver, string sound)
    {
        mSoundSystem?.PlaySound(sound, receiver);
    }
}

internal class ParryData
{
    public int windowStart;
    public int windowEnd;
    public float yawLeft;
    public float yawRight;
    public float pitchBottom;
    public float pitchTop;

    public ParryData(JsonObject definition)
    {
        JsonObject window = definition["window"];
        JsonObject direction = definition["direction"];
        windowStart = window["start"].AsInt();
        windowEnd = window["end"].AsInt();
        yawLeft = direction["left"].AsFloat() * GameMath.DEG2RAD;
        yawRight = direction["right"].AsFloat() * GameMath.DEG2RAD;
        pitchBottom = direction["bottom"].AsFloat() * GameMath.DEG2RAD;
        pitchTop = direction["top"].AsFloat() * GameMath.DEG2RAD;
    }

    public IResist AddToBehavior(ITempResistEntityBehavior behavior)
    {
        BlockOrParryResist resist = new((pitchTop, pitchBottom, yawLeft, yawRight));
        int duration = windowEnd - windowStart;
        behavior.AddResist(resist, duration > 0 ? duration : 1);
        return resist;
    }
}
