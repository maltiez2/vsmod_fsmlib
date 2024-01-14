using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

internal interface ISound
{
    void Play(IWorldAccessor world, Entity target);
}

internal sealed class SingleSound : ISound
{
    private readonly AssetLocation mLocation;
    private readonly float mRange = 32;
    private readonly float mVolume = 1;
    private readonly bool mRandomizePitch = true;

    public SingleSound(JsonObject definition)
    {
        mLocation = new AssetLocation(definition["location"].AsString());
        if (definition.KeyExists("range")) mRange = definition["range"].AsFloat(32);
        if (definition.KeyExists("volume")) mVolume = definition["volume"].AsFloat(1.0f);
        if (definition.KeyExists("randomizePitch")) mRandomizePitch = definition["randomizePitch"].AsBool(false);
    }
    public void Play(IWorldAccessor world, Entity target)
    {
        world.PlaySoundAt(mLocation, target, null, mRandomizePitch, mRange, mVolume);
    }
}

internal sealed class RandomizedSound : ISound
{
    private readonly static Random sRand = new();
    private readonly List<AssetLocation> mLocations = new();
    private readonly float mRange = 32;
    private readonly float mVolume = 1;
    private readonly bool mRandomizePitch = true;

    public RandomizedSound(JsonObject definition)
    {
        foreach (string path in definition["location"].AsArray<string>())
        {
            mLocations.Add(new AssetLocation(path));
        }

        if (definition.KeyExists("range")) mRange = definition["range"].AsFloat(32);
        if (definition.KeyExists("volume")) mVolume = definition["volume"].AsFloat(1.0f);
        if (definition.KeyExists("randomizePitch")) mRandomizePitch = definition["randomizePitch"].AsBool(false);
    }
    public void Play(IWorldAccessor world, Entity target)
    {
        int locationIndex = (int)Math.Floor((decimal)(sRand.NextDouble() * (mLocations.Count - 1)));
        world.PlaySoundAt(mLocations[locationIndex], target, null, mRandomizePitch, mRange, mVolume);
    }
}

internal sealed class TimedSound
{
    public TimeSpan Time { get; set; }
    public ISound Sound { get; set; }

    public TimedSound(TimeSpan time, ISound sound)
    {
        Time = time;
        Sound = sound;
    }
}

internal sealed class SoundSequenceTimer
{
    private readonly List<TimedSound> mSounds;
    private readonly IWorldAccessor mWorld;
    private readonly Entity mTarget;
    private long mCurrentTimer;
    private int mCurrentSound;
    private int mCurrentMilliseconds = 0;

    public SoundSequenceTimer(IWorldAccessor world, Entity target, List<TimedSound> sounds)
    {
        mSounds = sounds;
        mWorld = world;
        mTarget = target;

        mCurrentSound = 0;
        mCurrentMilliseconds = 0;
        mCurrentTimer = mWorld.RegisterCallback(Play, (int)mSounds[mCurrentSound].Time.TotalMilliseconds);
    }
    private void Play(float dt)
    {
        mSounds[mCurrentSound].Sound.Play(mWorld, mTarget);
        mCurrentSound++;
        if (mCurrentSound >= mSounds.Count)
        {
            Stop();
            return;
        }
        mCurrentTimer = mWorld.RegisterCallback(Play, (int)(mSounds[mCurrentSound].Time.TotalMilliseconds - dt * 1000 - mCurrentMilliseconds));
        mCurrentMilliseconds += (int)mSounds[mCurrentSound].Time.TotalMilliseconds;
    }
    public void Stop()
    {
        mWorld.UnregisterCallback(mCurrentTimer);
        mCurrentSound = 0;
        mCurrentMilliseconds = 0;
    }
}

internal sealed class SoundSequence
{
    private readonly List<TimedSound> mSounds = new();

    public SoundSequence(JsonObject definition, Action<string> logger)
    {
        int counter = -1;
        foreach (JsonObject sound in definition.AsArray())
        {
            counter++;

            if (!sound.KeyExists("time"))
            {
                logger.Invoke($"sound with index '{counter}' in sound sequence definition does not contain 'time' field");
                continue;
            }
            
            if (!sound["time"].IsArray())
            {
                TimedSound timedSound = new(TimeSpan.FromMilliseconds(sound["time"].AsInt(0)), ConstructSound(sound));
                mSounds.Add(timedSound);
                continue;
            }

            foreach (JsonObject time in sound["time"].AsArray())
            {
                TimedSound timedSound = new(TimeSpan.FromMilliseconds(time.AsInt(0)), ConstructSound(sound));
                mSounds.Add(timedSound);
            }
        }

        mSounds.Sort((first, second) => TimeSpan.Compare(first.Time, second.Time));
    }
    public SoundSequenceTimer Play(IWorldAccessor world, Entity target)
    {
        return new SoundSequenceTimer(world, target, mSounds);
    }
    private static ISound ConstructSound(JsonObject definition)
    {
        if (definition["location"].IsArray())
        {
            return new RandomizedSound(definition);
        }
        else
        {
            return new SingleSound(definition);
        }
    }
}


public class Sounds : BaseSystem, ISoundSystem
{
    private readonly Dictionary<string, ISound> mSounds = new();
    private readonly Dictionary<string, SoundSequence> mSequences = new();
    private readonly Dictionary<(long entityId, string code), SoundSequenceTimer> mTimers = new();

    public Sounds(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        if (definition.Token is not JObject sounds)
        {
            LogError($"Wrong definition format");
            return;
        }

        foreach ((string soundCode, JToken? soundDefinition) in sounds)
        {
            if (soundDefinition == null) continue;

            if (soundDefinition is JArray)
            {
                mSequences.Add(soundCode, new(new JsonObject(soundDefinition), message => LogError($"Sound '{soundCode}': {message}")));
            }
            else if (soundDefinition is JObject)
            {
                mSounds.Add(soundCode, ConstructSound(new JsonObject(soundDefinition)));
            }
        }
    }

    private static ISound ConstructSound(JsonObject definition)
    {
        if (definition["location"].IsArray())
        {
            return new RandomizedSound(definition);
        }
        else
        {
            return new SingleSound(definition);
        }
    }

    public override bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Verify(slot, player, parameters)) return false;

        if (
            parameters.KeyExists("sound") &&
            (
                mSounds.ContainsKey(parameters["sound"].AsString()) ||
                mSequences.ContainsKey(parameters["sound"].AsString())
            )
        )
        {
            return true;
        }

        return false;
    }
    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        if (mApi.Side != EnumAppSide.Server) return true;

        string? soundCode = parameters["sound"].AsString();
        string action = parameters["action"].AsString("play");

        if (soundCode == null)
        {
            LogError($"No 'sound' in system request");
            return false;
        }

        switch (action)
        {
            case "play":
                PlaySound(soundCode, player);
                break;
            case "stop":
                StopSound(soundCode, player);
                break;
            default:
                LogActions(action, "play", "stop");
                return false;
        }

        return true;
    }
    public void PlaySound(string soundCode, IPlayer player)
    {
        PlaySound(soundCode, player.Entity);
    }
    public void PlaySound(string soundCode, Entity target)
    {
        if (mApi.Side != EnumAppSide.Server) return;

        if (mSounds.ContainsKey(soundCode))
        {
            mSounds[soundCode].Play(mApi.World, target);
        }
        else if (mSequences.ContainsKey(soundCode))
        {
            SoundSequenceTimer timer = mSequences[soundCode].Play(mApi.World, target);
            if (mTimers.ContainsKey((target.EntityId, soundCode)))
            {
                mTimers[(target.EntityId, soundCode)].Stop();
            }
            mTimers[(target.EntityId, soundCode)] = timer;
        }
    }
    public void StopSound(string soundCode, IPlayer player)
    {
        StopSound(soundCode, player.Entity);
    }
    public void StopSound(string soundCode, Entity target)
    {
        if (mApi.Side != EnumAppSide.Server || mSequences.ContainsKey(soundCode)) return;

        if (mTimers.ContainsKey((target.EntityId, soundCode)))
        {
            mTimers[(target.EntityId, soundCode)].Stop();
            mTimers.Remove((target.EntityId, soundCode));
        }
    }
}
