using ImGuiNET;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public interface ISound : IDebugWindow
{
    ISoundSequenceTimer? Play(IWorldAccessor world, Entity target);
}

public interface ISoundSequenceTimer
{
    void Stop();
}

public interface ISoundEffectsManager : IDebugWindow
{
    ISound? Get(string code, string domain);
}

public class SoundEffectsManager : ISoundEffectsManager
{
    private readonly Dictionary<string, ISound> mSounds = new();

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
                mSounds.Add($"{domain}:{code}", new SoundSequence(new JsonObject(soundDefinition), message => { }));
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

    private readonly string mFilter = "";
    private int mSelected;

    public void Draw(string id)
    {
#if DEBUG
        IEnumerable<string> list = mSounds.Select(entry => entry.Key);
        VSImGui.TextEditor.ListWithFilter($"Sounds##{id}", mFilter, list, ref mSelected, out string selected);
        ImGui.SeparatorText($"Sound: '{selected}'");
        mSounds[selected].Draw(id);
#endif
    }
}

internal sealed class SingleSound : ISound
{
#if DEBUG
    private AssetLocation mLocation;
    private float mRange = 32;
    private float mVolume = 1;
    private bool mRandomizePitch = true;
    private readonly AssetLocation mDefaultLocation = new AssetLocation("game", "sounds/test");
#else
    private readonly AssetLocation mLocation;
    private readonly float mRange = 32;
    private readonly float mVolume = 1;
    private readonly bool mRandomizePitch = true;
#endif

    public SingleSound(JsonObject definition)
    {
        mLocation = new AssetLocation(definition["location"].AsString());
        if (definition.KeyExists("range")) mRange = definition["range"].AsFloat(32);
        if (definition.KeyExists("volume")) mVolume = definition["volume"].AsFloat(1.0f);
        if (definition.KeyExists("randomizePitch")) mRandomizePitch = definition["randomizePitch"].AsBool(false);
    }

    public void Draw(string id)
    {
#if DEBUG
        ImGui.DragFloat($"Range##{id}", ref mRange);
        ImGui.DragFloat($"Volume##{id}", ref mVolume);
        ImGui.Checkbox($"Randomize pitch##{id}", ref mRandomizePitch);
        ImGui.Text("Asset:");
        ImGui.Indent();
        VSImGui.AssetLocationEditor.Edit($"File##{id}", ref mLocation, mDefaultLocation);
        ImGui.Unindent();
#endif
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
#if DEBUG
    private List<AssetLocation> mLocations = new();
    private float mRange = 32;
    private float mVolume = 1;
    private bool mRandomizePitch = true;
#else
    private readonly List<AssetLocation> mLocations = new();
    private readonly float mRange = 32;
    private readonly float mVolume = 1;
    private readonly bool mRandomizePitch = true;
#endif

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

#if DEBUG
    private int mSelectedLocation = 0;
    private readonly AssetLocation mDefaultLocation = new AssetLocation("game", "sounds/test");
#endif

    public void Draw(string id)
    {
#if DEBUG
        ImGui.DragFloat($"Range##{id}", ref mRange);
        ImGui.DragFloat($"Volume##{id}", ref mVolume);
        ImGui.Checkbox($"Randomize pitch##{id}", ref mRandomizePitch);

        string[] locations = mLocations.Select(location => location.ToString()).ToArray();
        VSImGui.ListEditor.Edit($"Files##{id}", locations, ref mSelectedLocation, (_, index) => mLocations.RemoveAt(index), index =>
        {
            if (mLocations.Count > 0)
            {
                mLocations.Insert(index + 1, mDefaultLocation);
            }
            else
            {
                mLocations.Add(mDefaultLocation);
            }
            return "";
        });
        if (mLocations.Count > 0)
        {
            ImGui.Text("Asset:");
            ImGui.Indent();
            mLocations[mSelectedLocation] = VSImGui.AssetLocationEditor.Edit($"Location##{id}", mLocations[mSelectedLocation], mDefaultLocation);
            ImGui.Unindent();
        }
#endif
    }

    public ISoundSequenceTimer? Play(IWorldAccessor world, Entity target)
    {
        int locationIndex = (int)Math.Floor((decimal)(sRand.NextDouble() * (mLocations.Count - 1)));
        world.PlaySoundAt(mLocations[locationIndex], target, null, mRandomizePitch, mRange, mVolume);
        return null;
    }
}

internal sealed class TimedSound : IDebugWindow
{
    public TimeSpan Time { get; set; }
    public ISound Sound { get; set; }

#if DEBUG
    public ITimeEditor? TimeEditor { get; set; }
#endif

    public TimedSound(TimeSpan time, ISound sound)
    {
        Time = time;
        Sound = sound;
    }

    public void Draw(string id)
    {
#if DEBUG
        Time = TimeEditor?.Edit($"Trigger time##{id}", Time) ?? Time;
        Sound.Draw(id);
#endif
    }
}

internal interface ITimeEditor
{
    TimeSpan Edit(string title, TimeSpan value);
}

internal sealed class TimeFloatEditor : ITimeEditor
{
    private int mScale = 1;
    public TimeSpan Edit(string title, TimeSpan value)
    {
#if DEBUG
        VSImGui.TimeEditor.WithScaleSelection(title, ref value, ref mScale);
#endif
        return value;
    }
}

internal sealed class TimeFrameEditor : ITimeEditor
{
    private readonly float mDuration;
    private readonly float mOffset;
    private readonly int mTotal;

    public TimeFrameEditor(float duration, float offset, int total)
    {
        mDuration = duration;
        mOffset = offset;
        mTotal = total;
    }

    public TimeSpan Edit(string title, TimeSpan value)
    {
        float value_f = (float)value.TotalMilliseconds;
        float progress = (value_f - mOffset) / mDuration;
        int frame = (int)(progress * mTotal);
#if DEBUG
        ImGui.SliderInt(title, ref frame, 0, mTotal);
#endif
        progress = frame / (float)mTotal;
        value_f = mOffset + mDuration * progress;
        return TimeSpan.FromMilliseconds(value_f);
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

            if (sound["time"]?.KeyExists("frames") == true)
            {
                int framesNumber = sound["time"]["total"].AsInt();
                float duration = sound["time"]["duration"].AsFloat();
                float offset = sound["time"]["offset"].AsFloat();

                foreach (JsonObject time in sound["time"]["frames"].AsArray())
                {
                    float frame = time.AsInt(0);
                    float frameTime = offset + duration * frame / framesNumber;
                    TimedSound timedSound = new(TimeSpan.FromMilliseconds(frameTime), ConstructSound(sound));
                    mSounds.Add(timedSound);
#if DEBUG
                    mSounds[^1].TimeEditor = new TimeFrameEditor(duration, offset, framesNumber);
#endif
                }

                continue;
            }

            if (!sound["time"].IsArray())
            {
                TimedSound timedSound = new(TimeSpan.FromMilliseconds(sound["time"].AsInt(0)), ConstructSound(sound));
                mSounds.Add(timedSound);
#if DEBUG
                mSounds[^1].TimeEditor = new TimeFloatEditor();
#endif
                continue;
            }

            foreach (JsonObject time in sound["time"].AsArray())
            {
                TimedSound timedSound = new(TimeSpan.FromMilliseconds(time.AsInt(0)), ConstructSound(sound));
                mSounds.Add(timedSound);
#if DEBUG
                mSounds[^1].TimeEditor = new TimeFloatEditor();
#endif
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

#if DEBUG
    private int mSelected = 0;
#endif

    public void Draw(string id)
    {
#if DEBUG
        ImGui.SliderInt($"Index##{id}", ref mSelected, 0, mSounds.Count - 1);
        mSounds[mSelected].Draw(id);
#endif
    }
}