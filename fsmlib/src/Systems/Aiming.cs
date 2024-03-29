﻿using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace MaltiezFSM.Systems;

public class Aiming : BaseSystem, IAimingSystem
{
    private static readonly Random sRand = new();

    private readonly float mDispersionMin;
    private readonly float mDispersionMax;
    private readonly TimeSpan mAimTime;
    private readonly string mDescription;
    private readonly string mTimeAttrName;
    private readonly StatsModifier? mMinAccuracyStat;
    private readonly StatsModifier? mMaxAccuracyStat;
    private readonly StatsModifier? mAimingTime;
    private readonly Dictionary<long, float> mAimingTimes = new();
    private readonly Dictionary<long, float> mMinDispersions = new();
    private readonly Dictionary<long, float> mMaxDispersions = new();
    private readonly Dictionary<long, Utils.DelayedCallback> mTimers = new();
    private readonly string? mSoundSystemName;
    private readonly string? mSound;
    private ISoundSystem? mSoundSystem;
    private bool mIsAiming = false;


    public Aiming(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mDispersionMin = definition["dispersionMin_MOA"].AsFloat();
        mDispersionMax = definition["dispersionMax_MOA"].AsFloat();
        mAimTime = TimeSpan.FromMilliseconds(definition["duration"].AsInt());
        mDescription = definition["description"].AsString();
        mTimeAttrName = "fsmlib." + code + ".timePassed";
        if (definition.KeyExists("dispersionMin_stats")) mMinAccuracyStat = new(mApi, definition["dispersionMin_stats"].AsString());
        if (definition.KeyExists("dispersionMax_stats")) mMaxAccuracyStat = new(mApi, definition["dispersionMax_stats"].AsString());
        if (definition.KeyExists("duration_stats")) mAimingTime = new(mApi, definition["duration_stats"].AsString());
        if (definition.KeyExists("soundSystem")) mSoundSystemName = definition["soundSystem"].AsString();
        if (definition.KeyExists("sound")) mSound = definition["sound"].AsString();
    }

    public override void SetSystems(Dictionary<string, ISystem> systems)
    {
        if (mSoundSystemName != null)
        {
            if (!systems.ContainsKey(mSoundSystemName) || systems[mSoundSystemName] is not ISoundSystem)
            {
                IEnumerable<string> soundSystems = systems.Where((entry, _) => entry.Value is ISoundSystem).Select((entry, _) => entry.Key);
                LogError($"Sound system '{mSoundSystemName}' not found. Available sound systems: {Utils.PrintList(soundSystems)}.");
                return;
            }

            mSoundSystem = systems[mSoundSystemName] as ISoundSystem;
        }
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string? action = parameters["action"].AsString("start");

        switch (action)
        {
            case "start":
                player.Entity.Attributes.SetInt("aiming-noreticle", 1);
                WriteStartTimeTo(slot, mApi.World.ElapsedMilliseconds);
                SetAimingTime(player);
                SetDispersions(player);
                StartSoundTimer(slot, player);
                mIsAiming = true;
                break;
            case "stop":
                player.Entity.Attributes.SetInt("aiming-noreticle", 0);
                WriteStartTimeTo(slot, 0);
                StopSoundTimer(player);
                mIsAiming = false;
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }
        return true;
    }

    public Utils.DirectionOffset GetShootingDirectionOffset(ItemSlot slot, IPlayer player)
    {
        long currentTime = mApi.World.ElapsedMilliseconds;
        float aimProgress = mIsAiming ? Math.Clamp((currentTime - ReadStartTimeFrom(slot)) / mAimingTimes[player.Entity.EntityId], 0, 1) : 0;
        float aimingInaccuracy = Math.Max(0.001f, 1f - player.Entity.Attributes.GetFloat("aimingAccuracy"));
        float dispersion = GetDispersion(aimProgress, player) * aimingInaccuracy;
        float randomPitch = (float)(2 * (sRand.NextDouble() - 0.5) * (Math.PI / 180 / 60) * dispersion);
        float randomYaw = (float)(2 * (sRand.NextDouble() - 0.5) * (Math.PI / 180 / 60) * dispersion);
        return (randomPitch, randomYaw);
    }
    public TimeSpan GetAimingDuration(ItemSlot slot, IPlayer player)
    {
        long entityId = player.Entity.EntityId;
        if (mAimingTimes.ContainsKey(entityId))
        {
            return TimeSpan.FromSeconds(mAimingTimes[entityId]);
        }

        if (mAimingTime != null)
        {
            return mAimingTime.CalcSeconds(player, mAimTime);
        }

        return mAimTime;
    }
    public override string[]? GetDescription(ItemSlot slot, IWorldAccessor world)
    {
        if (mDescription == null) return null;

        return new string[] { Lang.Get(mDescription, (float)mAimTime.TotalSeconds, mDispersionMin, mDispersionMax) };
    }

    private void StartSoundTimer(ItemSlot slot, IPlayer player)
    {
        if (mSoundSystem == null || mSound == null) return;
        long entityId = player.Entity.EntityId;
        if (mTimers.ContainsKey(entityId)) mTimers[entityId].Dispose();
        mTimers[entityId] = new(mApi, GetAimingDuration(slot, player), () => mSoundSystem?.PlaySound(mSound, player));
    }
    private void StopSoundTimer(IPlayer player)
    {
        long entityId = player.Entity.EntityId;
        if (mTimers.ContainsKey(entityId)) mTimers[entityId].Cancel();
    }
    private void SetAimingTime(IPlayer player)
    {
        long entityId = player.Entity.EntityId;
        if (!mAimingTimes.ContainsKey(entityId)) mAimingTimes.Add(entityId, 0);
        if (mAimingTime != null)
        {
            mAimingTimes[entityId] = mAimingTime.Calc(player, (float)mAimTime.TotalSeconds);
        }
        else
        {
            mAimingTimes[entityId] = (float)mAimTime.TotalSeconds;
        }
    }
    private void SetDispersions(IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        if (!mMinDispersions.ContainsKey(entityId)) mMinDispersions.Add(entityId, 0);
        if (mMinAccuracyStat != null)
        {
            mMinDispersions[entityId] = mMinAccuracyStat.Calc(player, mDispersionMin);
        }
        else
        {
            mMinDispersions[entityId] = mDispersionMin;
        }

        if (!mMaxDispersions.ContainsKey(entityId)) mMaxDispersions.Add(entityId, 0);
        if (mMaxAccuracyStat != null)
        {
            mMaxDispersions[entityId] = mMaxAccuracyStat.Calc(player, mDispersionMin);
        }
        else
        {
            mMaxDispersions[entityId] = mDispersionMin;
        }
    }
    private float GetDispersion(float progress, IPlayer player)
    {
        long entityId = player.Entity.EntityId;

        return Math.Max(0, mMaxDispersions[entityId] - (mMaxDispersions[entityId] - mMinDispersions[entityId]) * progress);
    }
    private void WriteStartTimeTo(ItemSlot slot, long time)
    {
        slot?.Itemstack?.Attributes.SetLong(mTimeAttrName, time);
        slot?.MarkDirty();
    }
    private long ReadStartTimeFrom(ItemSlot slot)
    {
        long? startTime = slot?.Itemstack?.Attributes?.GetLong(mTimeAttrName, 0);
        return startTime == null || startTime == 0 ? mApi.World.ElapsedMilliseconds : startTime.Value;
    }
}

public class EntityBehaviorAimingAccuracyNoReticle : EntityBehavior
{
    public Random Rand { get; set; }
    public bool IsAiming { get; set; }

    private readonly List<AccuracyModifier> mModifiers = new();

    public EntityBehaviorAimingAccuracyNoReticle(Entity entity)
        : base(entity)
    {
        EntityAgent entityAgent = entity as EntityAgent;
        mModifiers.Add(new BaseAimingAccuracy(entityAgent));
        mModifiers.Add(new MovingAimingAccuracy(entityAgent));
        mModifiers.Add(new SprintAimingAccuracy(entityAgent));
        mModifiers.Add(new OnHurtAimingAccuracy(entityAgent));
        entity.Attributes.RegisterModifiedListener("aiming-noreticle", OnAimingChanged);
        Rand = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));
    }

    private void OnAimingChanged()
    {
        bool isAiming = IsAiming;
        IsAiming = entity.Attributes.GetInt("aiming") > 0;
        if (isAiming == IsAiming)
        {
            return;
        }

        if (IsAiming && entity.World is IServerWorldAccessor)
        {
            double value = Rand.NextDouble() - 0.5;
            double value2 = Rand.NextDouble() - 0.5;
            entity.WatchedAttributes.SetDouble("aimingRandPitch", value);
            entity.WatchedAttributes.SetDouble("aimingRandYaw", value2);
        }

        for (int i = 0; i < mModifiers.Count; i++)
        {
            if (IsAiming)
            {
                mModifiers[i].BeginAim();
            }
            else
            {
                mModifiers[i].EndAim();
            }
        }
    }

    public override void OnGameTick(float deltaTime)
    {
        if (IsAiming)
        {
            if (!entity.Alive)
            {
                entity.Attributes.SetInt("aiming-noreticle", 0);
            }

            float accuracy = 0f;
            for (int i = 0; i < mModifiers.Count; i++)
            {
                mModifiers[i].Update(deltaTime, ref accuracy);
            }

            entity.Attributes.SetFloat("aimingAccuracy-noReticle", accuracy);
        }
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        base.OnEntityReceiveDamage(damageSource, ref damage);
        if (damageSource.Type != EnumDamageType.Heal)
        {
            for (int i = 0; i < mModifiers.Count; i++)
            {
                mModifiers[i].OnHurt(damage);
            }
        }
    }

    public override string PropertyName()
    {
        return "aimingaccuracy-noreticle";
    }
}