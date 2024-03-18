using MaltiezFSM.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MaltiezFSM.Framework.Simplified.Systems;

public sealed class Sounds : BaseSystem
{
    public Sounds(ICoreAPI api, string domain = "game") : base(api)
    {
        _domain = domain;
        _effectsManager = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().SoundEffects;
    }

    public void Play(IPlayer player, string code, bool clientSide = false)
    {
        EnumAppSide side = clientSide ? EnumAppSide.Client : EnumAppSide.Server;
        if (Api.Side != side) return;

        PlaySound(code, player);
    }
    public void Stop(IPlayer player, string code)
    {
        StopSound(code, player);
    }

    private readonly Dictionary<(long entityId, string code), ISoundSequenceTimer?> _timers = new();
    private readonly string _domain;
    private readonly ISoundEffectsManager? _effectsManager;

    private void PlaySound(string soundCode, IPlayer player)
    {
        PlaySound(soundCode, player.Entity);
    }
    private void PlaySound(string soundCode, Entity target)
    {
        ISound? sound = _effectsManager?.Get(soundCode, _domain);
        if (sound == null)
        {
            try
            {
                target.World.PlaySoundAt(new AssetLocation(_domain, soundCode), target);
            }
            catch (Exception exception)
            {
                LogWarn($"Sound with code '{soundCode}' amd domain '{_domain}' not found, or specified error occured during playing it directly");
                LogVerbose($"Sound with code '{soundCode}' amd domain '{_domain}' not found, or specified error occured during playing it directly.\nException:{exception}\n");
            }
            return;
        }
        PlaySound(soundCode, sound, target);
    }
    private void PlaySound(string soundCode, ISound? sound, Entity target)
    {
        ISoundSequenceTimer? timer = sound?.Play(Api.World, target);

        if (_timers.ContainsKey((target.EntityId, soundCode)))
        {
            _timers[(target.EntityId, soundCode)]?.Stop();
        }
        _timers[(target.EntityId, soundCode)] = timer;
    }
    private void StopSound(string soundCode, IPlayer player)
    {
        StopSound(soundCode, player.Entity);
    }
    private void StopSound(string soundCode, Entity target)
    {
        if (_timers.ContainsKey((target.EntityId, soundCode)))
        {
            _timers[(target.EntityId, soundCode)]?.Stop();
            _timers.Remove((target.EntityId, soundCode));
        }
    }
}