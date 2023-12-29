using MaltiezFSM.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable enable

namespace MaltiezFSM.Systems;

internal interface ISound
{
    void Play(IPlayer player);
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
    public void Play(IPlayer player)
    {
        player.Entity.World.PlaySoundAt(mLocation, player.Entity, null, mRandomizePitch, mRange, mVolume);
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
    public void Play(IPlayer player)
    {
        int locationIndex = (int)Math.Floor((decimal)(sRand.NextDouble() * (mLocations.Count - 1)));
        player.Entity.World.PlaySoundAt(mLocations[locationIndex], player.Entity, null, mRandomizePitch, mRange, mVolume);
    }
}

internal sealed class TimedSound
{
    public TimeSpan Time { get; set; }
    public ISound Sound { get; set; }
}

internal sealed class SoundSequenceTimer
{
    private readonly List<TimedSound> mSounds;
    private readonly ICoreAPI mApi;
    private readonly IPlayer mPlayer;
    private long mCurrentTimer;
    private int mCurrentSound;

    public SoundSequenceTimer(ICoreAPI api, IPlayer player, List<TimedSound> sounds)
    {
        mSounds = sounds;
        mPlayer = player;
        mApi = api;

        mCurrentSound = 0;
        mCurrentTimer = mApi.World.RegisterGameTickListener(Play, (int)mSounds[mCurrentSound].Time.TotalMilliseconds);
    }
    private void Play(float dt)
    {
        mSounds[mCurrentSound].Sound.Play(mPlayer);
        mCurrentSound++;
        if (mCurrentSound >= mSounds.Count) return;
        mCurrentTimer = mApi.World.RegisterGameTickListener(Play, (int)(mSounds[mCurrentSound].Time.TotalMilliseconds - dt * 1000));
    }
    public void Stop()
    {
        mApi.World.UnregisterCallback(mCurrentTimer);
    }
}

internal sealed class SoundSequence
{
    private readonly List<TimedSound> mSounds = new();

    public SoundSequence(JsonObject definition)
    {
        foreach (JsonObject sound in definition.AsArray())
        {
            TimedSound timedSound = new()
            {
                Time = TimeSpan.FromMilliseconds(sound["time"].AsInt(0)),
                Sound = ConstructSound(sound)
            };
            mSounds.Add(timedSound);
        }

        mSounds.Sort((first, second) => TimeSpan.Compare(first.Time, second.Time));
    }
    public SoundSequenceTimer Play(ICoreAPI api, IPlayer player)
    {
        return new SoundSequenceTimer(api, player, mSounds);
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
        if (definition["sounds"].Token is not JObject sounds)
        {
            Utils.Logger.Error(mApi, this, $"Sounds system {mCode} has wrong definition format");
            return;
        }
        
        foreach ((string soundCode, JToken? soundDefinition) in sounds)
        {
            if (soundDefinition == null) continue;
            
            if (soundDefinition is JArray)
            {
                mSequences.Add(soundCode, new(new JsonObject(soundDefinition)));                                                                                                                                                                                          
            }
            else
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
        
        string soundCode = parameters["sound"].AsString();
        string action = parameters["action"].AsString("play");

        switch (action)
        {
            case "play":
                PlaySound(soundCode, player);
                break;
            case "stop":
                StopSound(soundCode, player);
                break;
        }

        return true;
    }
    public void PlaySound(string soundCode, IPlayer player)
    {
        if (mApi.Side != EnumAppSide.Server) return;

        if (mSounds.ContainsKey(soundCode))
        {
            mSounds[soundCode].Play(player);
        }
        else if (mSequences.ContainsKey(soundCode))
        {
            var timer = mSequences[soundCode].Play(mApi, player);
            mTimers.Add((player.Entity.EntityId, soundCode), timer);
        }
    }
    public void StopSound(string soundCode, IPlayer player)
    {
        if (mApi.Side != EnumAppSide.Server || mSequences.ContainsKey(soundCode)) return;

        if (mTimers.ContainsKey((player.Entity.EntityId, soundCode)))
        {
            mTimers[(player.Entity.EntityId, soundCode)].Stop();
        }
    }
}
