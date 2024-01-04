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

public class Block<TResistBehavior> : BaseSystem
    where TResistBehavior : EntityBehavior, ITempResistEntityBehavior
{
    private readonly Dictionary<long, IResist> mResists = new();
    private readonly Dictionary<long, long> mCallbacks = new();
    private readonly Dictionary<string, BlockData> mParries = new();
    private readonly Dictionary<string, string> mSounds = new();
    private readonly string? mSoundSystemName;
    private ISoundSystem? mSoundSystem;

    public Block(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mSoundSystemName = definition["soundSystem"].AsString();

        if (definition.Token is not JObject blocks) return;

        foreach ((string blockCode, JToken? block) in blocks)
        {
            if (block is not JObject) continue;
            JsonObject blockObject = new(block);
            mParries.Add(blockCode, new(blockObject));
            mSounds.Add(blockCode, blockObject["sound"].AsString());
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
                string code = parameters["block"].AsString();
                ScheduleBlock(player, code);
                break;
            case "stop":
                if (mApi.Side != EnumAppSide.Server) return true;
                StopBlock(player);
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }
        return true;
    }

    private void ScheduleBlock(IPlayer player, string code)
    {
        long entityId = player.Entity.EntityId;
        if (mCallbacks.ContainsKey(entityId)) mApi.World.UnregisterCallback(mCallbacks[entityId]);
        if (mParries[code].delay > 0)
        {
            mCallbacks[entityId] = mApi.World.RegisterCallback(_ => StartBlock(player, code), mParries[code].delay);
        }
        else
        {
            StartBlock(player, code);
        }
    }

    private void StartBlock(IPlayer player, string code)
    {
        if (mResists.ContainsKey(player.Entity.EntityId)) StopBlock(player);
        ITempResistEntityBehavior behavior = player.Entity.GetBehavior<TResistBehavior>();
        if (behavior == null)
        {
            LogError($"No IResistEntityBehavior found at {player.PlayerName} ({player.Entity.Class}:{player.Entity.Code})");
            return;
        }
        IResist resist = mParries[code].AddToBehavior(behavior);
        if (mSounds[code] != null) resist.SetResistCallback((IResist resist, DamageSource damageSource, ref float damage, Entity receiver) => PlayBlockSound(receiver, mSounds[code]));
        mResists.Add(player.Entity.EntityId, resist);
    }

    private void StopBlock(IPlayer player)
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

    private void PlayBlockSound(Entity player, string sound)
    {
        mSoundSystem?.PlaySound(sound, player);
    }
}

internal class BlockData
{
    public const int timeoutMs = 60000;

    public int delay;
    public float yawLeft = -180 * GameMath.DEG2RAD;
    public float yawRight = 180 * GameMath.DEG2RAD;
    public float pitchBottom = -180 * GameMath.DEG2RAD;
    public float pitchTop = 180 * GameMath.DEG2RAD;
    public Utils.DamageModifiers.TieredModifier? damageModifier = null;

    public BlockData(JsonObject definition)
    {
        delay = definition["delay"].AsInt(0);
        if (definition.KeyExists("modifier")) damageModifier = Utils.DamageModifiers.GetTiered(definition["modifier"]);

        if (definition.KeyExists("direction"))
        {
            JsonObject direction = definition["direction"];
            yawLeft = direction["left"].AsFloat() * GameMath.DEG2RAD;
            yawRight = direction["right"].AsFloat() * GameMath.DEG2RAD;
            pitchBottom = direction["bottom"].AsFloat() * GameMath.DEG2RAD;
            pitchTop = direction["top"].AsFloat() * GameMath.DEG2RAD;
        }
    }

    public IResist AddToBehavior(ITempResistEntityBehavior behavior)
    {
        BlockOrParryResist resist = new((pitchTop, pitchBottom, yawLeft, yawRight), false, new() { damageModifier });
        behavior.AddResist(resist, timeoutMs);
        return resist;
    }
}
