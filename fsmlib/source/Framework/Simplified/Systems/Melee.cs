using AnimationManagerLib;
using MaltiezFSM.API;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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

    public LineSegment(Vector3 position, Vector3 direction)
    {
        Position = position;
        Direction = direction;
    }
    public LineSegment(JsonObject json)
    {
        Position = new(json["X1"].AsFloat(0), json["Y1"].AsFloat(0), json["Z1"].AsFloat(0));
        Direction = new(json["X2"].AsFloat(0), json["Y2"].AsFloat(0), json["Z2"].AsFloat(0));
        Direction -= Position;
    }

    public void RenderAsLine(ICoreClientAPI api, EntityPlayer entityPlayer, int color = ColorUtil.WhiteArgb)
    {
        BlockPos playerPos = entityPlayer.Pos.AsBlockPos;
        Vector3 playerPosVector = new(playerPos.X, playerPos.Y, playerPos.Z);

        Vector3 tail = Position - playerPosVector;
        Vector3 head = Position + Direction - playerPosVector;

        api.Render.RenderLine(playerPos, tail.X, tail.Y, tail.Z, head.X, head.Y, head.Z, color);
    }
    public LineSegment? Transform(EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return null;

        return TransformSegment(this, modelMatrix, playerPos);
    }

    public bool RoughIntersect(Cuboidf collisionBox)
    {
        if (collisionBox.MaxX < Position.X && collisionBox.MaxX < (Position.X + Direction.X)) return false;
        if (collisionBox.MinX > Position.X && collisionBox.MinX > (Position.X + Direction.X)) return false;

        if (collisionBox.MaxY < Position.Y && collisionBox.MaxY < (Position.Y + Direction.Y)) return false;
        if (collisionBox.MinY > Position.Y && collisionBox.MinY > (Position.Y + Direction.Y)) return false;

        if (collisionBox.MaxZ < Position.Z && collisionBox.MaxZ < (Position.Z + Direction.Z)) return false;
        if (collisionBox.MinZ > Position.Z && collisionBox.MinZ > (Position.Z + Direction.Z)) return false;

        return true;
    }
    public Vector3? IntersectCylinder(CylinderCollisionBox box)
    {
        Vector3 distance = new(Position.X - box.CenterX, Position.Y - box.CenterY, 0);

        // Compute coefficients of the quadratic equation
        float a = (Direction.X * Direction.X) / (box.RadiusX * box.RadiusX) + (Direction.Y * Direction.Y) / (box.RadiusY * box.RadiusY);
        float b = 2 * ((distance.X * Direction.X) / (box.RadiusX * box.RadiusX) + (distance.Y * Direction.Y) / (box.RadiusY * box.RadiusY));
        float c = (distance.X * distance.X) / (box.RadiusX * box.RadiusX) + (distance.Y * distance.Y) / (box.RadiusY * box.RadiusY) - 1;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0 || a == 0) return null;

        float intersectionPointPositionInSegment1 = (-b + MathF.Sqrt(discriminant)) / (2 * a);
        float intersectionPointPositionInSegment2 = (-b - MathF.Sqrt(discriminant)) / (2 * a);

        float intersectionPointZ1 = Position.Z + intersectionPointPositionInSegment1 * Direction.Z;
        float intersectionPointZ2 = Position.Z + intersectionPointPositionInSegment2 * Direction.Z;

        float minZ = Math.Min(intersectionPointZ1, intersectionPointZ2);
        float maxZ = Math.Max(intersectionPointZ1, intersectionPointZ2);

        if (!(minZ <= box.BottomZ && maxZ >= box.TopZ)) return null;

        float closestIntersectionPoint = MathF.Min(intersectionPointPositionInSegment1, intersectionPointPositionInSegment2);

        return Position + Direction * closestIntersectionPoint;
    }
    public Vector3? IntersectCuboid(Cuboidf collisionBox)
    {
        float tMin = 0.0f;
        float tMax = 1.0f;

        if (!CheckAxisIntersection(Direction.X, Position.X, collisionBox.MinX, collisionBox.MaxX, ref tMin, ref tMax)) return null;
        if (!CheckAxisIntersection(Direction.Y, Position.Y, collisionBox.MinY, collisionBox.MaxY, ref tMin, ref tMax)) return null;
        if (!CheckAxisIntersection(Direction.Z, Position.Z, collisionBox.MinZ, collisionBox.MaxZ, ref tMin, ref tMax)) return null;

        return Position + tMin * Direction;
    }
    public (Block, Vector3)? IntersectTerrain(ICoreClientAPI api)
    {
        int minX = (int)MathF.Min(Position.X, Position.X + Direction.X);
        int minY = (int)MathF.Min(Position.Y, Position.Y + Direction.Y);
        int minZ = (int)MathF.Min(Position.Z, Position.Z + Direction.Z);

        int maxX = (int)MathF.Max(Position.X, Position.X + Direction.X);
        int maxY = (int)MathF.Max(Position.Y, Position.Y + Direction.Y);
        int maxZ = (int)MathF.Max(Position.Z, Position.Z + Direction.Z);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    (Block, Vector3)? intersection = IntersectBlock(api.World.BlockAccessor, x, y, z);
                    if (intersection != null) return intersection;
                }
            }
        }

        return null;
    }

    public static IEnumerable<LineSegment> Transform(IEnumerable<LineSegment> segments, EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return Array.Empty<LineSegment>();

        return segments.Select(segment => TransformSegment(segment, modelMatrix, playerPos));
    }
    
    private static readonly Vec4f _inputBuffer = new(0, 0, 0, 1);
    private static readonly Vec4f _outputBuffer = new(0, 0, 0, 1);
    private static readonly Matrixf _matrixBuffer = new();
    private readonly BlockPos _blockPosBuffer = new();
    private readonly Vec3d _blockPosVecBuffer = new();
    private const float _epsilon = 1e-6f;

    private static LineSegment TransformSegment(LineSegment value, Matrixf modelMatrix, EntityPos playerPos)
    {
        Vector3 tail = TransformVector(value.Position, modelMatrix, playerPos);
        Vector3 head = TransformVector(value.Direction + value.Position, modelMatrix, playerPos);

        return new(tail, head - tail);
    }
    private static Vector3 TransformVector(Vector3 value, Matrixf modelMatrix, EntityPos playerPos)
    {
        _inputBuffer.X = value.X;
        _inputBuffer.Y = value.Y;
        _inputBuffer.Z = value.Z;

        Mat4f.MulWithVec4(modelMatrix.Values, _inputBuffer, _outputBuffer);

        _outputBuffer.X += (float)playerPos.X;
        _outputBuffer.Y += (float)playerPos.Y;
        _outputBuffer.Z += (float)playerPos.Z;

        return new(_outputBuffer.X, _outputBuffer.Y, _outputBuffer.Z);
    }
    private static Matrixf? GetHeldItemModelMatrix(EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        if (entity.Properties.Client.Renderer is not EntityShapeRenderer entityShapeRenderer) return null;

        ItemStack? itemStack = itemSlot?.Itemstack;
        if (itemStack == null) return null;

        AttachmentPointAndPose? attachmentPointAndPose = entity.TpAnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
        if (attachmentPointAndPose == null) return null;

        AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
        ItemRenderInfo itemStackRenderInfo = api.Render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, 0f);
        if (itemStackRenderInfo?.Transform == null) return null;

        return _matrixBuffer.Set(entityShapeRenderer.ModelMat).Mul(attachmentPointAndPose.AnimModelMatrix).Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
            .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
            .Translate(attachPoint.PosX / 16.0 + itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + itemStackRenderInfo.Transform.Translation.Z)
            .RotateX((float)(attachPoint.RotationX + itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
            .RotateY((float)(attachPoint.RotationY + itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
            .RotateZ((float)(attachPoint.RotationZ + itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f))
            .Translate(0f - itemStackRenderInfo.Transform.Origin.X, 0f - itemStackRenderInfo.Transform.Origin.Y, 0f - itemStackRenderInfo.Transform.Origin.Z);
    }
    private static bool CheckAxisIntersection(float dirComponent, float startComponent, float minComponent, float maxComponent, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(dirComponent) < _epsilon)
        {
            // Ray is parallel to the slab, check if it's within the slab's extent
            if (startComponent < minComponent || startComponent > maxComponent) return false;
        }
        else
        {
            // Calculate intersection distances to the slab
            float t1 = (minComponent - startComponent) / dirComponent;
            float t2 = (maxComponent - startComponent) / dirComponent;

            // Swap t1 and t2 if needed so that t1 is the intersection with the near plane
            if (t1 > t2)
            {
                (t2, t1) = (t1, t2);
            }

            // Update the minimum intersection distance
            tMin = MathF.Max(tMin, t1);
            // Update the maximum intersection distance
            tMax = MathF.Min(tMax, t2);

            // Early exit if intersection is not possible
            if (tMin > tMax) return false;
        }

        return true;
    }
    private (Block, Vector3)? IntersectBlock(IBlockAccessor blockAccessor, int x, int y, int z)
    {
        Block block = blockAccessor.GetBlock(x, y, z, BlockLayersAccess.MostSolid);
        _blockPosBuffer.Set(x, y, z);

        Cuboidf[] collisionBoxes = block.GetCollisionBoxes(blockAccessor, _blockPosBuffer);
        if (collisionBoxes == null || collisionBoxes.Length == 0) return null;

        _blockPosVecBuffer.Set(x, y, z);
        for (int i = 0; i < collisionBoxes.Length; i++)
        {
            Cuboidf? collBox = collisionBoxes[i];
            if (collBox == null) continue;

            Vector3? intersection = IntersectCuboid(collBox);

            if (intersection == null) continue;

            return (block, intersection.Value);
        }

        return null;
    }
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
        RadiusX = collisionBox.MaxX - collisionBox.MinX;
        RadiusY = collisionBox.MaxY - collisionBox.MinY;
        TopZ = collisionBox.MaxX;
        BottomZ = collisionBox.MinZ;
        CenterX = (collisionBox.MaxX + collisionBox.MinX) / 2;
        CenterY = (collisionBox.MaxY + collisionBox.MinY) / 2;
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
