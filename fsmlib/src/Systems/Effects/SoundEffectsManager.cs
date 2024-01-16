using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public interface ISound
{
    ISoundSequenceTimer? Play(IWorldAccessor world, Entity target);
}

public interface ISoundSequenceTimer
{
    void Stop();
}

public interface ISoundEffectsManager
{
    ISound? Get(string code, string domain);
}

public class SoundEffectsManager : ISoundEffectsManager
{
    private readonly Dictionary<string, ISound> mSounds = new();
    private readonly Dictionary<string, SoundSequence> mSequences = new();

    public SoundEffectsManager(ICoreAPI api)
    {
        List<IAsset> assets = api.Assets.GetManyInCategory("config", "sound-effects.json");

        foreach (IAsset asset in assets)
        {
            string domain = asset.Location.Domain;
            byte[] data = asset.Data;
            string json = System.Text.Encoding.UTF8.GetString(data);
            JObject token = JObject.Parse(json);

            LoadSounds(domain, token);
        }
    }

    public ISound? Get(string code, string domain)
    {
        string key = $"{domain}:{code}";
        if (!mSounds.ContainsKey(key)) return null;
        return mSounds[key];
    }

    private void LoadSounds(string domain, JObject sounds)
    {
        foreach ((string code, JToken? soundDefinition) in sounds)
        {
            if (soundDefinition == null) continue;

            if (soundDefinition is JArray)
            {
                mSequences.Add($"{domain}:{code}", new(new JsonObject(soundDefinition), message => { }));
            }
            else if (soundDefinition is JObject)
            {
                mSounds.Add($"{domain}:{code}", ConstructSound(new JsonObject(soundDefinition)));
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
    public ISoundSequenceTimer? Play(IWorldAccessor world, Entity target)
    {
        world.PlaySoundAt(mLocation, target, null, mRandomizePitch, mRange, mVolume);
        return null;
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
    public ISoundSequenceTimer? Play(IWorldAccessor world, Entity target)
    {
        int locationIndex = (int)Math.Floor((decimal)(sRand.NextDouble() * (mLocations.Count - 1)));
        world.PlaySoundAt(mLocations[locationIndex], target, null, mRandomizePitch, mRange, mVolume);
        return null;
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

internal sealed class SoundSequenceTimer : ISoundSequenceTimer
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

internal sealed class SoundSequence : ISound
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
    public ISoundSequenceTimer Play(IWorldAccessor world, Entity target)
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