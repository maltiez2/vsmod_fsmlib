using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework.Simplified.Systems;

internal static class MeleeSynchronizer
{
    public const string NetworkChannelId = "fsmlib:melee-damage-sync";

    public static void Init(ICoreClientAPI api)
    {
        _clientChannel = api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeAttackPacket>()
            .RegisterMessageType<MeleeCollisionPacket>();
    }
    public static void Init(ICoreServerAPI api)
    {
        _api = api;
        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<MeleeAttackPacket>()
            .RegisterMessageType<MeleeCollisionPacket>()
            .SetMessageHandler<MeleeAttackPacket>(HandlePacket)
            .SetMessageHandler<MeleeCollisionPacket>(HandlePacket);
    }
    public static void Send(MeleeAttackPacket packet)
    {
        _clientChannel?.SendPacket(packet);
    }
    public static void Send(MeleeCollisionPacket packet)
    {
        _clientChannel?.SendPacket(packet);
    }

    internal static IClientNetworkChannel? _clientChannel;
    internal static ICoreServerAPI? _api;
    internal static readonly Dictionary<(long playerId, long attackId), (Action<MeleeCollisionPacket> attack, float range)> _attacks = new();
    private const float _rangeFactor = 2.0f;

    private static void HandlePacket(IServerPlayer player, MeleeCollisionPacket packet)
    {
        (long, long) id = (player.Entity.EntityId, packet.Id);
        if (_attacks.ContainsKey(id))
        {
            _attacks[id].attack?.Invoke(packet);
        }
    }
    private static void HandlePacket(IServerPlayer player, MeleeAttackPacket packet)
    {
        (long PlayerId, long AttackId) attackId = (packet.PlayerId, packet.AttackId);

        if (!_attacks.ContainsKey(attackId)) return;

        int range = (int)Math.Ceiling(_attacks[attackId].range * _rangeFactor);

        foreach (MeleeAttackDamagePacket damagePacket in packet.Damages)
        {
            ApplyDamage(damagePacket, range);
        }
    }
    private static void ApplyDamage(MeleeAttackDamagePacket packet, int range)
    {
        if (_api == null) return;

        Entity target = _api.World.GetEntityById(packet.Target);
        Entity attacker = _api.World.GetEntityById(packet.CauseEntity);

        if (!target.ServerPos.InRangeOf(attacker.ServerPos, range * range))
        {
            return;
        }

        target.ReceiveDamage(new DamageSource()
        {
            Source = packet.Source,
            SourceEntity = null,
            CauseEntity = attacker,
            Type = packet.DamageType,
            DamageTier = packet.DamageTier
        }, packet.Damage);

        Vec3f knockback = new(packet.Knockback);
        target.SidedPos.Motion.X *= packet.Stagger;
        target.SidedPos.Motion.Y *= packet.Stagger;
        target.SidedPos.Motion.Add(knockback);
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeAttackPacket
{
    public long AttackId { get; set; }
    public long PlayerId { get; set; }
    public MeleeAttackDamagePacket[] Damages { get; set; } = Array.Empty<MeleeAttackDamagePacket>();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeAttackDamagePacket
{
    public long Target { get; set; }
    public EnumDamageSource Source { get; set; }
    public long CauseEntity { get; set; }
    public EnumDamageType DamageType { get; set; }
    public int DamageTier { get; set; }
    public float Damage { get; set; }
    public float[] Knockback { get; set; } = Array.Empty<float>();
    public float Stagger { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class MeleeCollisionPacket
{
    public long Id { get; set; }
    public bool Finished { get; set; }
    public string[] Blocks { get; set; } = Array.Empty<string>();
    public long[] Entities { get; set; } = Array.Empty<long>();
}