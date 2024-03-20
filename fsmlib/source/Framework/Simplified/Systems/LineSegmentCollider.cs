using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MaltiezFSM.Framework.Simplified.Systems;

public readonly struct LineSegmentCollider
{
    public readonly Vector3 Position;
    public readonly Vector3 Direction;

    public LineSegmentCollider(Vector3 position, Vector3 direction)
    {
        Position = position;
        Direction = direction;
    }
    public LineSegmentCollider(JsonObject json)
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
    public LineSegmentCollider? Transform(EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
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
        Vector3 distance = new(Position.X - box.CenterX, Position.Z - box.CenterZ, 0);

        // Compute coefficients of the quadratic equation
        float a = (Direction.X * Direction.X) / (box.RadiusX * box.RadiusX) + (Direction.Z * Direction.Z) / (box.RadiusZ * box.RadiusZ);
        float b = 2 * ((distance.X * Direction.X) / (box.RadiusX * box.RadiusX) + (distance.Z * Direction.Z) / (box.RadiusZ * box.RadiusZ));
        float c = (distance.X * distance.X) / (box.RadiusX * box.RadiusX) + (distance.Z * distance.Z) / (box.RadiusZ * box.RadiusZ) - 1;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0 || a == 0) return null;

        float intersectionPointPositionInSegment1 = (-b + MathF.Sqrt(discriminant)) / (2 * a);
        float intersectionPointPositionInSegment2 = (-b - MathF.Sqrt(discriminant)) / (2 * a);

        float intersectionPointY1 = Position.Y + intersectionPointPositionInSegment1 * Direction.Y;
        float intersectionPointY2 = Position.Y + intersectionPointPositionInSegment2 * Direction.Y;

        float minY = Math.Min(intersectionPointY1, intersectionPointY2);
        float maxY = Math.Max(intersectionPointY1, intersectionPointY2);

        if (minY > box.TopY || maxY < box.BottomY) return null;

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

    public static IEnumerable<LineSegmentCollider> Transform(IEnumerable<LineSegmentCollider> segments, EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return Array.Empty<LineSegmentCollider>();

        return segments.Select(segment => TransformSegment(segment, modelMatrix, playerPos));
    }
    public static bool Transform(IEnumerable<MeleeAttackDamageType> segments, EntityPlayer entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
    {
        EntityPos playerPos = entity.Pos;
        Matrixf? modelMatrix = GetHeldItemModelMatrix(entity, itemSlot, api, right);
        if (modelMatrix is null) return false;

        foreach (MeleeAttackDamageType damageType in segments)
        {
            damageType._inWorldCollider = TransformSegment(damageType.Collider, modelMatrix, playerPos);
        }

        return true;
    }

    private static readonly Vec4f _inputBuffer = new(0, 0, 0, 1);
    private static readonly Vec4f _outputBuffer = new(0, 0, 0, 1);
    private static readonly Matrixf _matrixBuffer = new();
    private static readonly BlockPos _blockPosBuffer = new();
    private static readonly Vec3d _blockPosVecBuffer = new();
    private const float _epsilon = 1e-6f;

    private static LineSegmentCollider TransformSegment(LineSegmentCollider value, Matrixf modelMatrix, EntityPos playerPos)
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

        AttachmentPointAndPose? attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
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

            Cuboidf collBoxInWorld = collBox.Clone();
            collBoxInWorld.X1 += x;
            collBoxInWorld.Y1 += y;
            collBoxInWorld.Z1 += z;
            collBoxInWorld.X2 += x;
            collBoxInWorld.Y2 += y;
            collBoxInWorld.Z2 += z;

            Vector3? intersection = IntersectCuboid(collBoxInWorld);

            if (intersection == null) continue;

            return (block, intersection.Value);
        }

        return null;
    }
}

public readonly struct CylinderCollisionBox
{
    public readonly float RadiusX;
    public readonly float RadiusZ;
    public readonly float TopY;
    public readonly float BottomY;
    public readonly float CenterX;
    public readonly float CenterZ;

    public CylinderCollisionBox(Cuboidf collisionBox)
    {
        RadiusX = (collisionBox.MaxX - collisionBox.MinX) / 2;
        RadiusZ = (collisionBox.MaxZ - collisionBox.MinZ) / 2;
        TopY = collisionBox.MaxY;
        BottomY = collisionBox.MinY;
        CenterX = (collisionBox.MaxX + collisionBox.MinX) / 2;
        CenterZ = (collisionBox.MaxZ + collisionBox.MinZ) / 2;
    }
}