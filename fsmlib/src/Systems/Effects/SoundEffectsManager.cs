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
    JToken Serialize();
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
    private readonly ICoreAPI mApi;

    public SoundEffectsManager(ICoreAPI api)
    {
        mApi = api;
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
#if DEBUG
        if (mReload && mApi.Side == EnumAppSide.Server)
        {
            mReload = false;
            Load();
        }
#endif

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

#if DEBUG
    private readonly string mFilter = "";
    private int mSelected;
    private bool mJsonOutput = false;
    private Dictionary<string, string> mOutput = new();
    private static bool mReload = false;
#endif

    public void Draw(string id)
    {
#if DEBUG
        if (ImGui.Button($"Save to JSON##{id}"))
        {
            mOutput = Save();
            mJsonOutput = true;
        }
        ImGui.SameLine();
        if (ImGui.Button($"Load from JSON##{id}"))
        {
            Load();
            mReload = true;
        }
        ImGui.SameLine();
        if (ImGui.Button($"Reload##{id}"))
        {
            Save();
            Load();
            mReload = true;
        }
        if (mJsonOutput)
        {
            ImGui.Begin($"JSON output##{id}", ref mJsonOutput, ImGuiWindowFlags.Modal);
            foreach ((string domain, string output) in mOutput)
            {
                if (ImGui.CollapsingHeader($"{domain}##header{id}"))
                {
                    System.Numerics.Vector2 size = ImGui.GetWindowSize();
                    size.X -= 28;
                    size.Y -= 64;
                    string output_2 = output;
                    ImGui.InputTextMultiline($"##{id}", ref output_2, (uint)output.Length * 2, size, ImGuiInputTextFlags.ReadOnly);
                }
            }

            ImGui.End();
        }

        IEnumerable<string> list = mSounds.Select(entry => entry.Key);
        VSImGui.TextEditor.ListWithFilter($"Sounds##{id}", mFilter, list, ref mSelected, out string selected);
        ImGui.SeparatorText($"Sound: '{selected}'");
        mSounds[selected].Draw(id);
#endif
    }

#if DEBUG
    public Dictionary<string, string> Save()
    {
        JObject sounds = new(mSounds.Select(entry => new JProperty(entry.Key, entry.Value.Serialize())).ToArray());

        Dictionary<string, string> output = sounds
            .Properties()
            .GroupBy(property => property.Name.Split(":").FirstOrDefault("game"))
            .Select(entry => KeyValuePair.Create(
                        entry.Key,
                        (new JObject(entry.Select(property => new JProperty(property.Name.Split(":")[1], property.Value)).ToArray())).ToString()
                    )
                ).ToDictionary(entry => entry.Key, entry => entry.Value);

        mApi.StoreModConfig(new(sounds), "fsmlib-temp-sounds.json");

        return output;
    }

    public void Load()
    {
        if (mApi.LoadModConfig("fsmlib-temp-sounds.json").Token is not JObject sounds) return;

        mSounds.Clear();

        foreach ((string code, JToken? soundDefinition) in sounds)
        {
            if (soundDefinition == null) continue;

            if (soundDefinition is JArray)
            {
                mSounds.Add(code, new SoundSequence(new JsonObject(soundDefinition), message => { }));
            }
            else if (soundDefinition is JObject)
            {
                mSounds.Add(code, ConstructSound(new JsonObject(soundDefinition)));
            }
        }
    }
#endif
}

internal sealed class SingleSound : ISound
{
#if DEBUG
    private AssetLocation mLocation;
    private float mRange = 32;
    private float mVolume = 1;
    private bool mRandomizePitch = true;
    private readonly AssetLocation mDefaultLocation = new("game", "sounds/test");
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

    public JToken Serialize()
    {
        JObject result = new()
        {
            new JProperty("location", new JValue(mLocation.ToString())),
        };

        if (mRange != 32.0f) result.Add("range", new JValue(mRange));
        if (mVolume != 1.0f) result.Add("volume", new JValue(mVolume));
        if (mRandomizePitch) result.Add("randomizePitch", new JValue(true));

        return result;
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
    private readonly List<AssetLocation> mLocations = new();
#if DEBUG
    private float mRange = 32;
    private float mVolume = 1;
    private bool mRandomizePitch = true;
#else
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

    public JToken Serialize()
    {
        JValue[] locations = mLocations.Select(value => new JValue(value.ToString())).ToArray();

        JObject result = new()
        {
            new JProperty("location", new JArray(locations)),
        };

        if (mRange != 32.0f) result.Add("range", new JValue(mRange));
        if (mVolume != 1.0f) result.Add("volume", new JValue(mVolume));
        if (mRandomizePitch) result.Add("randomizePitch", new JValue(true));

        return result;
    }

#if DEBUG
    private int mSelectedLocation = 0;
    private readonly AssetLocation mDefaultLocation = new("game", "sounds/test");
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
    public ITimeEditor? TimeEditor { get; set; }

    public TimedSound(TimeSpan time, ISound sound)
    {
        Time = time;
        Sound = sound;
    }

    public JObject Serialize()
    {
        if (Sound.Serialize() is not JObject result) return new JObject();
        JToken? time = TimeEditor?.Serialize(Time);
        if (time != null) result.Add("time", time);
        return result;
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
    JToken Serialize(TimeSpan value);
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

    public JToken Serialize(TimeSpan value)
    {
        return new JValue((int)value.TotalMilliseconds);
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

    public JToken Serialize(TimeSpan value)
    {
        float value_f = (float)value.TotalMilliseconds;
        float progress = (value_f - mOffset) / mDuration;
        int frame = (int)(progress * mTotal);

        return new JObject()
        {
            new JProperty("total", new JValue(mTotal)),
            new JProperty("duration", new JValue((int)mDuration)),
            new JProperty("offset", new JValue((int)mOffset)),
            new JProperty("frames", new JArray() { new JValue(frame) })
        };
    }
}

internal sealed class SoundSequenceTimer : ISoundSequenceTimer
{
    private readonly List<TimedSound> mSounds;
    private readonly IWorldAccessor mWorld;
    private readonly Entity mTarget;
    private readonly List<long> mTimers = new();

    public SoundSequenceTimer(IWorldAccessor world, Entity target, List<TimedSound> sounds)
    {
        mSounds = sounds;
        mWorld = world;
        mTarget = target;

        Play();
    }
    private void Play()
    {
        foreach (TimedSound sound in mSounds)
        {
            int time = (int)sound.Time.TotalMilliseconds;
            long timer = mWorld.RegisterCallback(_ => sound.Sound.Play(mWorld, mTarget), time);
            mTimers.Add(timer);
        }
    }
    public void Stop()
    {
        foreach (long timer in mTimers)
        {
            mWorld.UnregisterCallback(timer);
        }
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
                    mSounds[^1].TimeEditor = new TimeFrameEditor(duration, offset, framesNumber);
                }

                continue;
            }

            if (!sound["time"].IsArray())
            {
                TimedSound timedSound = new(TimeSpan.FromMilliseconds(sound["time"].AsInt(0)), ConstructSound(sound));
                mSounds.Add(timedSound);
                mSounds[^1].TimeEditor = new TimeFloatEditor();
                continue;
            }

            foreach (JsonObject time in sound["time"].AsArray())
            {
                TimedSound timedSound = new(TimeSpan.FromMilliseconds(time.AsInt(0)), ConstructSound(sound));
                mSounds.Add(timedSound);
                mSounds[^1].TimeEditor = new TimeFloatEditor();
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

    public JToken Serialize()
    {
        JObject[] sounds = mSounds
            .Select(value => value.Serialize())
            .GroupBy(Comparator)
            .Where(value => value != null)
            .Select(pair => pair.Aggregate(Aggregate)).ToArray();

        return new JArray(sounds);
    }

    private JObject Aggregate(JObject first, JObject second)
    {
        if (first["time"] is JObject firstObject && second["time"] is JObject secondObject)
        {
            JArray firstValue = firstObject["frames"] is JArray firstArray ? firstArray : new JArray() { firstObject["frames"] ?? new JValue(0) };
            JArray secondValue = secondObject["frames"] is JArray secondArray ? secondArray : new JArray() { secondObject["frames"] ?? new JValue(0) };
            JArray resultArray = new(firstValue.Concat(secondValue).ToArray());

            firstObject["frames"]?.Replace(resultArray);
        }
        else
        {
            JArray firstValue = first["time"] is JArray firstArray ? firstArray : new JArray() { first["time"] ?? new JValue(0f) };
            JArray secondValue = second["time"] is JArray secondArray ? secondArray : new JArray() { second["time"] ?? new JValue(0f) };
            JArray resultArray = new(firstValue.Concat(secondValue).ToArray());

            first["time"]?.Replace(resultArray);
        }

        return first;
    }

    private int Comparator(JObject sound)
    {
        JsonObject soundObject = new(sound);

        float volume = soundObject["volume"].AsFloat(1.0f);
        float range = soundObject["range"].AsFloat(32.0f);
        bool pitch = soundObject["randomizePitch"].AsBool(false);
        string time = "";

        if (soundObject.KeyExists("time") && soundObject["time"].KeyExists("frames"))
        {
            int total = soundObject["total"].AsInt();
            int duration = soundObject["duration"].AsInt();
            int offset = soundObject["offset"].AsInt();
            time = $"{total}{duration}{offset}";
        }

        string forHash = $"{volume}{range}{pitch}{sound["location"]}{time}";

        return forHash.GetHashCode();
    }
}