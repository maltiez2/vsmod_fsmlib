using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework
{
    public class TransformsManager
    {
        private readonly ICoreAPI mApi;
        private readonly Dictionary<long, Dictionary<EnumItemRenderTarget, Dictionary<string, ModelTransform>>> mTransforms = new();

        public ModelTransform currentTransform { get; private set; }

        public TransformsManager(ICoreAPI api)
        {
            mApi = api;
        }

        public void SetTransform(long entityId, string transformType, EnumItemRenderTarget target, ModelTransform transform)
        {
            if (!mTransforms.ContainsKey(entityId)) mTransforms[entityId] = new();
            if (!mTransforms[entityId].ContainsKey(target)) mTransforms[entityId][target] = new();
            mTransforms[entityId][target][transformType] = transform;
        }

        public void ResetTransform(long entityId, string transformType, EnumItemRenderTarget target)
        {
            if (!mTransforms.ContainsKey(entityId)) mTransforms[entityId] = new();
            if (!mTransforms[entityId].ContainsKey(target)) mTransforms[entityId][target] = new();
            mTransforms[entityId][target][transformType] = Utils.IdentityTransform();
        }

        public ModelTransform GetTransform(long entityId, string transformType, EnumItemRenderTarget target)
        {
            if (!mTransforms.ContainsKey(entityId)) mTransforms[entityId] = new();
            if (!mTransforms[entityId].ContainsKey(target)) mTransforms[entityId][target] = new();
            if (!mTransforms[entityId][target].ContainsKey(transformType)) mTransforms[entityId][target][transformType] = Utils.IdentityTransform();
            return mTransforms[entityId][target][transformType];
        }

        public void CalcCurrentTransform(long entityId, EnumItemRenderTarget target)
        {
            currentTransform = Utils.IdentityTransform();
            if (!mTransforms.ContainsKey(entityId) || !mTransforms[entityId].ContainsKey(target)) return;
            
            foreach ((string code, ModelTransform transform) in mTransforms[entityId][target])
            {
                currentTransform = Utils.CombineTransforms(currentTransform, transform);
            }
        }
    }
}
