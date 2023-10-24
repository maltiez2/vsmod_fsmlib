using Bullseye;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using System.Linq;
using MaltiezFSM.Framework;

namespace MaltiezFSM.BullseyeCompatibility
{
    public class SmoothAnimationAttachableBehavior : BullseyeCollectibleBehaviorAnimatable // Based on code from TeacupAngel (https://github.com/TeacupAngel)
    {
        public SmoothAnimationAttachableBehavior(CollectibleObject collObj) : base(collObj)
        {
        }

        private readonly Dictionary<string, Attachment> mAttachments = new();
        private readonly Dictionary<string, bool> mActiveAttachments = new();

        public void AddAttachment(string attachmentCode, ItemStack attachmentItem, JsonObject transform)
        {
            mAttachments.Add(attachmentCode, new(capi, attachmentCode, attachmentItem, transform));
            mActiveAttachments.Add(attachmentCode, true);
        }

        public void ToggleAttachment(string attachmentCode, bool toggle)
        {
            mActiveAttachments[attachmentCode] = toggle;
        }

        public void RemoveAttachment(string attachmentCode)
        {
            mAttachments.Remove(attachmentCode);
            mActiveAttachments.Remove(attachmentCode);
        }

        public void ClearAttachments()
        {
            mAttachments.Clear();
            mActiveAttachments.Clear();
        }

        public override void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
        {
            base.RenderHandFp(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize);

            if (onlyWhenAnimating && ActiveAnimationsByAnimCode.Count == 0) return;

            foreach ((string code, bool active) in mActiveAttachments.Where(x => x.Value))
            {
                mAttachments[code].Render(renderInfo, Animator, modelMat);
            }
        }
    }

    public class Attachment
    {
        private readonly ICoreClientAPI mApi;
        private readonly ItemRenderInfo mAttachedRenderInfo;
        private readonly string mAttachmentPointCode;

        private Matrixf mAttachedMeshMatrix = new Matrixf();

        public Attachment(ICoreClientAPI api, string attachmentPointCode, ItemStack attachment, JsonObject transform)
        {
            mApi = api;
            mAttachedRenderInfo = GetAttachmentRenderInfo(attachment, transform);
            mAttachmentPointCode = attachmentPointCode;
        }

        public void Render(ItemRenderInfo renderInfo, AnimatorBase animator, Matrixf modelMat)
        {
            if (mAttachedRenderInfo == null) return;
            if (mApi == null) return;
            
            IShaderProgram prog = mApi.Render.CurrentActiveShader;

            AttachmentPointAndPose attachmentPointAndPose = animator.GetAttachmentPointPose(mAttachmentPointCode);
            AttachmentPoint attachmentPoint = attachmentPointAndPose.AttachPoint;
            CalculateMeshMatrix(modelMat, renderInfo, attachmentPointAndPose, attachmentPoint);
            prog.UniformMatrix("modelViewMatrix", mAttachedMeshMatrix.Values);

            mApi.Render.RenderMesh(mAttachedRenderInfo.ModelRef);
        }

        private void CalculateMeshMatrix(Matrixf modelMat, ItemRenderInfo renderInfo, AttachmentPointAndPose apap, AttachmentPoint ap)
        {
            mAttachedMeshMatrix = modelMat.Clone()
                .Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
                .Mul(apap.AnimModelMatrix)
                .Translate((ap.PosX + mAttachedRenderInfo.Transform.Translation.X) / 16f, (ap.PosY + mAttachedRenderInfo.Transform.Translation.Y) / 16f, (ap.PosZ + mAttachedRenderInfo.Transform.Translation.Z) / 16f)
                .Translate(renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z)
                .RotateX((float)(ap.RotationX) * GameMath.DEG2RAD)
                .RotateY((float)(ap.RotationY) * GameMath.DEG2RAD)
                .RotateZ((float)(ap.RotationZ) * GameMath.DEG2RAD)
                .Scale(mAttachedRenderInfo.Transform.ScaleXYZ.X, mAttachedRenderInfo.Transform.ScaleXYZ.Y, mAttachedRenderInfo.Transform.ScaleXYZ.Z)
                .Translate(-mAttachedRenderInfo.Transform.Origin.X / 16f, -mAttachedRenderInfo.Transform.Origin.Y / 16f, -mAttachedRenderInfo.Transform.Origin.Z / 16f)
                .Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
            ;
        }

        private ItemRenderInfo GetAttachmentRenderInfo(ItemStack attachment, JsonObject transform)
        {
            DummySlot dummySlot = new DummySlot(attachment);
            ItemRenderInfo renderInfo = mApi.Render.GetItemStackRenderInfo(dummySlot, EnumItemRenderTarget.Ground);
            renderInfo.Transform = Utils.ToTransformFrom(transform); // Utils.CombineTransforms(renderInfo.Transform, transform).Clone();
            return renderInfo;
        }
    }
}
