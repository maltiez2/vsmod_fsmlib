using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;


public class Sounds : BaseSystem, ISoundSystem
{
    private readonly Dictionary<(long entityId, string code), ISoundSequenceTimer?> mTimers = new();
    private readonly string mDomain;
    private readonly ISoundEffectsManager? mEffectsManager;

    public Sounds(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        string? domain = definition["domain"].AsString();
        if (domain == null)
        {
            LogError($"No domain was specified");
        }
        mDomain = domain ?? "fsmlib";

        mEffectsManager = mApi.ModLoader.GetModSystem<FiniteStateMachineSystem>().SoundEffects;
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
        ISound? sound = mEffectsManager?.Get(soundCode, mDomain);
        if (sound == null)
        {
            LogWarn($"Sound with code '{soundCode}' amd domain '{mDomain}' not found");
            return;
        }
        PlaySound(soundCode, sound, target);
    }
    public void PlaySound(string soundCode, ISound? sound, IPlayer player)
    {
        PlaySound(soundCode, sound, player.Entity);
    }
    public void PlaySound(string soundCode, ISound? sound, Entity target)
    {
        if (mApi.Side != EnumAppSide.Server) return;

        ISoundSequenceTimer? timer = sound?.Play(mApi.World, target);

        if (mTimers.ContainsKey((target.EntityId, soundCode)))
        {
            mTimers[(target.EntityId, soundCode)]?.Stop();
        }
        mTimers[(target.EntityId, soundCode)] = timer;
    }
    public void StopSound(string soundCode, IPlayer player)
    {
        StopSound(soundCode, player.Entity);
    }
    public void StopSound(string soundCode, Entity target)
    {
        if (mApi.Side != EnumAppSide.Server) return;

        if (mTimers.ContainsKey((target.EntityId, soundCode)))
        {
            mTimers[(target.EntityId, soundCode)]?.Stop();
            mTimers.Remove((target.EntityId, soundCode));
        }
    }
}
