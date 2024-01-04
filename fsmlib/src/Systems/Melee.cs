using MaltiezFSM.API;
using MaltiezFSM.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;



namespace MaltiezFSM.Systems;

public sealed class Melee : BaseSystem
{
    private readonly Dictionary<string, MeleeAttack> mAttacks = new();
    private readonly Dictionary<long, Utils.TickBasedTimer?> mTimers = new();
    private readonly string mSoundSystemCode;
    private bool mSystemsSetUp = false;

    public Melee(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mSoundSystemCode = definition["soundSystem"].AsString();

        if (definition.KeyExists("attack") && definition["attack"].KeyExists("code"))
        {
            mAttacks.Add(definition["attack"]["code"].AsString(), new(definition["attack"], api));
            return;
        }

        if (!definition.KeyExists("attacks") || definition["attacks"].Token is not JObject attacks)
        {
            LogError($"Received attacks data in wrong format or haven't received it at all");
            return;
        }

        foreach ((string attackCode, JToken? attack) in attacks)
        {
            if (attack == null) continue;
            mAttacks.Add(attackCode, new(new JsonObject(attack), api));
        }
    }

    public override void SetSystems(Dictionary<string, ISystem> systems)
    {
        if (!systems.ContainsKey(mSoundSystemCode) || systems[mSoundSystemCode] is not ISoundSystem soundSystem)
        {
            LogError($"Sound system '{mSoundSystemCode}' was not found while setting systems for '{mCode}'");
            return;
        }

        mSystemsSetUp = true;
        foreach ((_, MeleeAttack attack) in mAttacks)
        {
            attack.SetSystems(soundSystem);
        }
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!mSystemsSetUp) return false;
        if (!base.Process(slot, player, parameters)) return false;

        long playerId = player.Entity.EntityId;
        if (!mTimers.ContainsKey(playerId)) mTimers.Add(playerId, null);
        mTimers[playerId]?.Stop();

        string? action = parameters["action"].AsString();
        string? attack = parameters["attack"].AsString();

        if (action == null)
        {
            LogError($"No 'action' in system request");
            return false;
        }

        if (attack == null)
        {
            LogError($"No 'attack' in system request");
            return false;
        }

        if (!mAttacks.ContainsKey(attack))
        {
            LogError($"Attack with code '{attack}' was not found");
            return false;
        }

        switch (action)
        {
            case "start":
                if (mApi is ICoreServerAPI serverApi)
                {
                    player.Entity.Attributes.SetInt("didattack", 0);
                    mTimers[playerId] = new(mApi, mAttacks[attack].GetDuration(player), (float progress) => mAttacks[attack].TryAttack(slot, player, serverApi, progress));
                }
                break;
            case "stop":
                if (mApi is ICoreServerAPI)
                {
                    mTimers[playerId]?.Stop();
                }
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }
        return true;
    }
}

internal sealed class MeleeAttack
{
    private readonly HitWindow mHitWindow;
    private readonly List<AttackDamageType> mDamageTypes = new();
    private readonly ICustomInputInvoker? mCustomInputInvoker;
    private readonly string? mCustomInput = null;
    private readonly bool mStopOnHandled = false;
    private readonly TimeSpan mDuration;
    private readonly StatsModifier? mDurationModifier;

    public MeleeAttack(JsonObject definition, ICoreAPI api)
    {
        mDuration = TimeSpan.FromMilliseconds(definition["duration"].AsInt());
        mHitWindow = new(definition);
        mCustomInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().CustomInputInvoker;
        if (definition.KeyExists("hitInput")) mCustomInput = definition["hitInput"].AsString();
        if (definition.KeyExists("stopOnInput")) mStopOnHandled = definition["stopOnInput"].AsBool();
        if (definition.KeyExists("duration_stats")) mDurationModifier = new(api, definition["duration_stats"].AsString());

        foreach (JsonObject damageType in definition["damageTypes"].AsArray())
        {
            mDamageTypes.Add(new(damageType, api));
        }
    }

    public void SetSystems(ISoundSystem soundSystem)
    {
        foreach (AttackDamageType damageType in mDamageTypes)
        {
            damageType.SetSoundSystem(soundSystem);
        }
    }
    public bool TryAttack(ItemSlot slot, IPlayer player, ICoreServerAPI api, float attackProgress)
    {
        if (!mHitWindow.Check(attackProgress, mDuration)) return false;

        if (player.Entity.Attributes.GetInt("didattack") == 0 && Attack(player, api))
        {
            if (InvokeInput(slot, player)) return false;
            player.Entity.Attributes.SetInt("didattack", 1);
            return true;
        }

        return false;
    }
    public TimeSpan GetDuration(IPlayer player)
    {
        if (mDurationModifier == null) return mDuration;

        return mDurationModifier.CalcMilliseconds(player, mDuration);
    }

    private bool InvokeInput(ItemSlot slot, IPlayer player)
    {
        if (mCustomInput == null) return false;

        bool handled = mCustomInputInvoker?.Invoke(mCustomInput, player, slot) == true;

        return mStopOnHandled && handled;
    }
    private bool Attack(IPlayer player, ICoreServerAPI api)
    {
        Entity? target = player.Entity.EntitySelection?.Entity;

        if (target == null) return false;

        if (!CheckIfCanAttack(player, target, api)) return false;

        float distance = GetDistance(player.Entity, target);

        bool successfullyHit = false;

        foreach (AttackDamageType damageType in mDamageTypes.Where(damageType => damageType.Attack(player, target, distance)))
        {
            successfullyHit = true;
        }

        return successfullyHit;
    }
    static private bool CheckIfCanAttack(IPlayer attacker, Entity target, ICoreServerAPI api)
    {
        if (attacker is not IServerPlayer serverAttacker) return false;
        if (target is EntityPlayer && (!api.Server.Config.AllowPvP || !serverAttacker.HasPrivilege("attackplayers"))) return false;
        if (target is IPlayer && !serverAttacker.HasPrivilege("attackcreatures")) return false;
        return true;
    }
    static private float GetDistance(EntityPlayer attacker, Entity target)
    {
        Cuboidd hitbox = target.SelectionBox.ToDouble().Translate(target.Pos.X, target.Pos.Y, target.Pos.Z);
        EntityPos sidedPos = attacker.SidedPos;
        double x = sidedPos.X + attacker.LocalEyePos.X;
        double y = sidedPos.Y + attacker.LocalEyePos.Y;
        double z = sidedPos.Z + attacker.LocalEyePos.Z;
        return (float)hitbox.ShortestDistanceFrom(x, y, z);
    }
}

internal readonly struct HitWindow
{
    public TimeSpan Start { get; }
    public TimeSpan? Finish { get; }

    public HitWindow(JsonObject definition)
    {
        Start = TimeSpan.FromMilliseconds(definition["hitWindowStart_ms"].AsInt(0));
        int finish = definition["hitWindowEnd_ms"].AsInt(-1);
        Finish = finish > 0 ? TimeSpan.FromMilliseconds(finish) : null;
    }

    public readonly bool Check(float progress, TimeSpan duration)
    {
        if (progress > 1 || progress < 0) return false;
        return Start <= progress * duration && progress * duration <= (Finish ?? duration);
    }
}

public sealed class AttackDamageType
{
    private readonly float mDamage;
    private readonly float mKnockback;
    private readonly string mSound;
    private readonly EnumDamageType mDamageType;
    private readonly Tuple<float, float> mReachWindow;
    private readonly StatsModifier? mDamageModifier;
    private readonly StatsModifier? mReachModifier;
    private readonly StatsModifier? mKnockbackModifier;

    private ISoundSystem? mSoundSystem;

    public AttackDamageType(JsonObject definition, ICoreAPI api)
    {
        mSound = definition["sound"].AsString();
        mDamage = definition["damage"].AsFloat();
        mKnockback = definition["knockback"].AsFloat(0);
        if (definition.KeyExists("minReach") || definition.KeyExists("maxReach"))
        {
            mReachWindow = new(definition["minReach"].AsFloat(0), definition["maxReach"].AsFloat(1));
        }
        else
        {
            mReachWindow = new(0, definition["reach"].AsFloat(1));
        }

        mDamageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), definition["type"].AsString("PiercingAttack"));

        if (definition.KeyExists("damage_stats")) mDamageModifier = new(api, definition["damage_stats"].AsString());
        if (definition.KeyExists("reach_stats")) mReachModifier = new(api, definition["reach_stats"].AsString());
        if (definition.KeyExists("knockback_stats")) mKnockbackModifier = new(api, definition["knockback_stats"].AsString());
    }

    public void SetSoundSystem(ISoundSystem system) => mSoundSystem = system;

    public bool Attack(IPlayer attacker, Entity target, float distance)
    {
        if (!(mReachWindow.Item1 < distance && distance < GetReach(attacker))) return false;

        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = mDamageType
        }, GetDamage(attacker));

        if (damageReceived)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) / 10f * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.Add(knockback);

            if (mSound != null) mSoundSystem?.PlaySound(mSound, target);
        }

        return damageReceived;
    }

    private float GetReach(IPlayer attacker)
    {
        if (mReachModifier == null) return mReachWindow.Item2;
        return mReachModifier.Calc(attacker, mReachWindow.Item2);
    }
    private float GetDamage(IPlayer attacker)
    {
        if (mDamageModifier == null) return mDamage;
        return mDamageModifier.Calc(attacker, mDamage);
    }
    private float GetKnockback(IPlayer attacker)
    {
        if (mKnockbackModifier == null) return mKnockback;
        return mKnockbackModifier.Calc(attacker, mKnockback);
    }
}
