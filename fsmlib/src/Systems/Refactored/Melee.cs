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

#nullable enable

namespace MaltiezFSM.Systems;

public sealed class Melee : BaseSystem
{
    private readonly Dictionary<string, MeleeAttack> mAttacks = new();
    private readonly Dictionary<long, Utils.TickBasedTimer?> mTimers = new();
    private readonly string mAnimationSystemCode;
    private readonly string mSoundSystemCode;
    private bool mSystemsSetUp = false;

    public Melee(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mAnimationSystemCode = definition["animationSystem"].AsString();
        mSoundSystemCode = definition["soundSystem"].AsString();

        if (definition.KeyExists("attack") && definition["attack"].KeyExists("code"))
        {
            mAttacks.Add(definition["attack"]["code"].AsString(), new(definition["attack"], api));
            return;
        }

        if (!definition.KeyExists("attacks") || definition["attacks"].Token is not JObject attacks)
        {
            Utils.Logger.Error(mApi, this, $"Melee system '{mCode}' received attacks data in wrong format or haven't received it at all");
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
        if (systems.ContainsKey(mAnimationSystemCode) || systems[mAnimationSystemCode] is not IAnimationSystem animationSystem)
        {
            Utils.Logger.Error(mApi, this, $"Animation system '{mAnimationSystemCode}' was not found while setting systems for '{mCode}'");
            return;
        }

        if (systems.ContainsKey(mSoundSystemCode) || systems[mSoundSystemCode] is not ISoundSystem soundSystem)
        {
            Utils.Logger.Error(mApi, this, $"Sound system '{mSoundSystemCode}' was not found while setting systems for '{mCode}'");
            return;
        }

        mSystemsSetUp = true;
        foreach ((_, MeleeAttack attack) in mAttacks)
        {
            attack.SetSystems(soundSystem, animationSystem);
        }
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!mSystemsSetUp) return false;
        if (!base.Process(slot, player, parameters)) return false;

        long playerId = player.Entity.EntityId;
        if (!mTimers.ContainsKey(playerId)) mTimers.Add(playerId, null);
        mTimers[playerId]?.Stop();

        string action = parameters["action"].AsString();
        string attack = parameters["attack"].AsString();

        if (!mAttacks.ContainsKey(attack))
        {
            Utils.Logger.Error(mApi, this, $"Attack with code '{attack}' was not found");
            return false;
        }

        switch (action)
        {
            case "start":
                if (mApi is ICoreServerAPI serverApi)
                {
                    player.Entity.Attributes.SetInt("didattack", 0);
                    mTimers[playerId] = new(mApi, mAttacks[attack].Duration, (float progress) => mAttacks[attack].TryAttack(slot, player, serverApi, progress));
                }
                mAttacks[attack].StartAnimation(slot, player);
                break;
            case "stop":
                if (mApi is ICoreServerAPI)
                {
                    mTimers[playerId]?.Stop();
                }
                mAttacks[attack].StopAnimation(slot, player);
                break;
            default:
                Utils.Logger.Error(mApi, this, $"Wrong action '{action}'. Available actions: 'start', 'stop'.");
                return false;
        }
        return true;
    }
}

internal sealed class MeleeAttack
{
    public TimeSpan Duration { get; }

    private readonly HitWindow mHitWindow;
    private readonly List<AttackDamageType> mDamageTypes = new();
    private readonly ICustomInputInvoker mCustomInputInvoker;
    private readonly string? mCustomInput = null;
    private readonly bool mStopOnHandled = false;
    private readonly string? mAnimationCode = null;
    private readonly string? mAnimationCategory = null;
    private IAnimationSystem? mAnimationPlayer;


    public MeleeAttack(JsonObject definition, ICoreAPI api)
    {
        Duration = TimeSpan.FromMilliseconds(definition["duration"].AsInt());
        mHitWindow = new(definition);
        mCustomInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().CustomInputInvoker;
        if (definition.KeyExists("hitInput")) mCustomInput = definition["hitInput"].AsString();
        if (definition.KeyExists("stopOnInput")) mStopOnHandled = definition["stopOnInput"].AsBool();
        if (definition.KeyExists("animationCode")) mAnimationCode = definition["animationCode"].AsString();
        if (definition.KeyExists("animationCategory")) mAnimationCategory = definition["animationCategory"].AsString();

        foreach (JsonObject damageType in definition["damageTypes"].AsArray())
        {
            mDamageTypes.Add(new(damageType, api));
        }
    }

    public void SetSystems(ISoundSystem soundSystem, IAnimationSystem animationSystem)
    {
        mAnimationPlayer = animationSystem;

        foreach (AttackDamageType damageType in mDamageTypes)
        {
            damageType.SetSoundSystem(soundSystem);
        }
    }
    public bool TryAttack(ItemSlot slot, IPlayer player, ICoreServerAPI api, float attackProgress)
    {
        if (!mHitWindow.Check(attackProgress, Duration)) return false;

        if (player.Entity.Attributes.GetInt("didattack") == 0 && Attack(player, api))
        {
            if (InvokeInput(slot, player)) return false;
            player.Entity.Attributes.SetInt("didattack", 1);
            return true;
        }

        return false;
    }
    public void StartAnimation(ItemSlot slot, IPlayer player)
    {
        if (mAnimationPlayer != null && mAnimationCode != null && mAnimationCategory != null)
        {
            mAnimationPlayer.PlayAnimation(slot, player, mAnimationCode, mAnimationCategory, "start");
        }
    }
    public void StopAnimation(ItemSlot slot, IPlayer player)
    {
        if (mAnimationPlayer != null && mAnimationCode != null && mAnimationCategory != null)
        {
            mAnimationPlayer.PlayAnimation(slot, player, mAnimationCode, mAnimationCategory, "stop");
        }
    }

    private bool InvokeInput(ItemSlot slot, IPlayer player)
    {
        if (mCustomInput == null) return false;

        bool handled = mCustomInputInvoker.Invoke(mCustomInput, player, slot);

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

internal sealed class AttackDamageType
{
    private readonly float mDamage;
    private readonly float mKnockback;
    private readonly string mSound;
    private readonly EnumDamageType mDamageType;
    private readonly Tuple<float, float> mReachWindow;
    private readonly List<Tuple<string, Utils.DamageModifiers.Modifier>> mModifiers = new();

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

        if (definition.KeyExists("stats"))
        {
            foreach (JsonObject stat in definition["stats"].AsArray())
            {
                mModifiers.Add(new(stat["code"].AsString(), Utils.DamageModifiers.Get(stat["type"].AsString("Multiply"))));
            }
        }
    }

    public void SetSoundSystem(ISoundSystem system) => mSoundSystem = system;

    public bool Attack(IPlayer attacker, Entity target, float distance)
    {
        if (!(mReachWindow.Item1 < distance && distance < mReachWindow.Item2)) return false;

        float damage = mDamage;

        foreach ((string stat, Utils.DamageModifiers.Modifier? modifier) in mModifiers)
        {
            modifier(ref damage, attacker.Entity.Stats.GetBlended(stat));
        }

        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = mDamageType
        }, damage);

        if (damageReceived)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * mKnockback / 10f * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.Add(knockback);

            if (mSound != null) mSoundSystem?.PlaySound(mSound, target);
        }

        return damageReceived;
    }
}
