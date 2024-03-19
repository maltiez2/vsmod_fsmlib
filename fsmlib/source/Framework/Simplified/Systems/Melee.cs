using MaltiezFSM.API;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework.Simplified.Systems;

public sealed class Melee : BaseSystem
{
    private readonly Dictionary<string, MeleeAttack> mAttacks = new();
    private readonly Dictionary<long, Utils.TickBasedTimer?> mTimers = new();

    public Melee(ICoreAPI api, string debugName = "") : base(api, debugName)
    {

    }

    public void CheckCollisions(IPlayer player)
    {
        if (Api is not ICoreClientAPI clientApi) return;

        AttachmentPointAndPose attachmentPoint = player.Entity.OtherAnimManager.Animator.GetAttachmentPointPose("RightHand");
        float[] modelMatrix = attachmentPoint.AnimModelMatrix;

        Vec3f tail = new(0, 0, 0);
        Vec3f head = new(0, 0, 1);

        Mat4f.MulWithVec3(modelMatrix, tail, tail);
        Mat4f.MulWithVec3(modelMatrix, head, head);

        clientApi.Render.RenderLine(player.Entity.Pos.AsBlockPos, tail.X, tail.Y, tail.Z, head.X, head.Y, head.Z, ColorUtil.WhiteArgb);
    }
}

public sealed class MeleeAttack
{
    private readonly HitWindow mHitWindow;
    private readonly List<AttackDamageType> mDamageTypes = new();
    private readonly ICustomInputInvoker? mCustomInputInvoker;
    private readonly string? mCustomInput = null;
    private readonly TimeSpan mDuration;
    private readonly StatsModifier? mDurationModifier;
    private readonly ICoreAPI mApi;

    public MeleeAttack(JsonObject definition, ICoreAPI api)
    {
        mApi = api;
        mDuration = TimeSpan.FromMilliseconds(definition["duration"].AsInt());
        mHitWindow = new(definition);
        mCustomInputInvoker = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().CustomInputInvoker;
        if (definition.KeyExists("hitInput")) mCustomInput = definition["hitInput"].AsString();
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

        if (player.Entity.Attributes.GetInt("didattack") == 0 && Attack(player, api, attackProgress))
        {
            InvokeInput(slot, player);
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

    private void InvokeInput(ItemSlot slot, IPlayer player)
    {
        if (mCustomInput == null) return;

        mApi.World.RegisterCallback(_ => mCustomInputInvoker?.Invoke(mCustomInput, player, slot), 0);
    }
    private bool Attack(IPlayer player, ICoreServerAPI api, float attackProgress)
    {
        Entity? target = player.Entity.EntitySelection?.Entity;

        if (target == null) return false;

        if (!CheckIfCanAttack(player, target, api)) return false;

        float distance = GetDistance(player.Entity, target);

        bool successfullyHit = false;

        foreach (AttackDamageType damageType in mDamageTypes.Where(damageType => damageType.Attack(player, target, distance, attackProgress)))
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

public readonly struct LineSegment
{
    public readonly Vector3 Position;
    public readonly Vector3 Direction;
}

public readonly struct CylinderCollisionBox
{
    public readonly float RadiusX;
    public readonly float RadiusY;
    public readonly float TopZ;
    public readonly float BottomZ;
    public readonly float CenterX;
    public readonly float CenterY;

    public CylinderCollisionBox(Cuboidf collisionBox)
    {
        RadiusX = collisionBox.X2 - collisionBox.X1;
        RadiusY = collisionBox.Y2 - collisionBox.Y1;
        TopZ = collisionBox.Z2;
        BottomZ = collisionBox.Z1;
        CenterX = (collisionBox.X2 + collisionBox.X1) / 2;
        CenterY = (collisionBox.Y2 + collisionBox.Y1) / 2;
    }

    public bool Intersects(LineSegment segment)
    {
        Vector3 distance = new(segment.Position.X - CenterX, segment.Position.Y - CenterY, 0);

        // Compute coefficients of the quadratic equation
        float a = (segment.Direction.X * segment.Direction.X) / (RadiusX * RadiusX) + (segment.Direction.Y * segment.Direction.Y) / (RadiusY * RadiusY);
        float b = 2 * ((distance.X * segment.Direction.X) / (RadiusX * RadiusX) + (distance.Y * segment.Direction.Y) / (RadiusY * RadiusY));
        float c = (distance.X * distance.X) / (RadiusX * RadiusX) + (distance.Y * distance.Y) / (RadiusY * RadiusY) - 1;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0 || a == 0)
        {
            // No intersection with cylinder
            return false;
        }

        // Compute intersection points along the line segment
        float t1 = (-b + (float)Math.Sqrt(discriminant)) / (2 * a);
        float t2 = (-b - (float)Math.Sqrt(discriminant)) / (2 * a);

        // Check if intersection points are within the height of the cylinder
        float z1 = segment.Position.Z + t1 * segment.Direction.Z;
        float z2 = segment.Position.Z + t2 * segment.Direction.Z;

        float minZ = Math.Min(z1, z2);
        float maxZ = Math.Max(z1, z2);

        return minZ <= BottomZ && maxZ >= TopZ;
    }
}

public sealed class AttackDamageType
{
    private readonly float mDamage;
    private readonly float mKnockback;
    private readonly int mTier;
    private readonly string mSound;
    private readonly EnumDamageType mDamageType;
    private readonly Tuple<float, float> mReachWindow;
    private readonly StatsModifier? mDamageModifier;
    private readonly StatsModifier? mReachModifier;
    private readonly StatsModifier? mKnockbackModifier;
    private readonly float mMinimalReachProgress;

    public AttackDamageType(JsonObject definition, ICoreAPI api)
    {
        mSound = definition["sound"].AsString();
        mDamage = definition["damage"].AsFloat();
        mKnockback = definition["knockback"].AsFloat(0);
        mTier = definition["tier"].AsInt(0);
        mMinimalReachProgress = definition["reachStart"].AsFloat(1);
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

    public bool Attack(IPlayer attacker, Entity target, float distance, float progress)
    {

        if (!(mReachWindow.Item1 < distance && distance < GetReach(attacker, progress))) return false;

        bool damageReceived = target.ReceiveDamage(new DamageSource()
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = null,
            CauseEntity = attacker.Entity,
            Type = mDamageType,
            DamageTier = mTier
        }, GetDamage(attacker));


        if (damageReceived)
        {
            Vec3f knockback = (target.Pos.XYZFloat - attacker.Entity.Pos.XYZFloat).Normalize() * GetKnockback(attacker) / 10f * target.Properties.KnockbackResistance;
            target.SidedPos.Motion.Add(knockback);
        }

        return damageReceived;
    }

    private float GetReach(IPlayer attacker, float progress)
    {
        float reachMultiplier = ((1 - mMinimalReachProgress) * progress + mMinimalReachProgress);
        if (mReachModifier == null) return mReachWindow.Item2 * reachMultiplier;
        return mReachModifier.Calc(attacker, mReachWindow.Item2) * reachMultiplier;
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
