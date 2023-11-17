using MaltiezFSM.API;
using ProtoBuf;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework
{
    public class TransformsManager : ITransformManager
    {
        public ModelTransform currentTransform { get; private set; }

        private const string entityIdAttr = "FSMlib.entityId";
        private readonly Dictionary<long, Dictionary<EnumItemRenderTarget, Dictionary<string, ModelTransform>>> mTransforms = new();

        private const string setChannelName = "FSMlib.setTranform";
        private const string resetChannelName = "FSMlib.resetTranform";
        private readonly TransformPacketSender mSetTransformSender;
        private readonly TransformPacketSender mResetTransformSender;

        public TransformsManager(ICoreAPI api)
        {
            mSetTransformSender = new TransformPacketSender(api, SetTransform, setChannelName);
            mResetTransformSender = new TransformPacketSender(api, (entityId, transformType, target, transform) => ResetTransform(entityId, transformType, target), resetChannelName);
        }

        public void SetTransform(long entityId, string transformType, EnumItemRenderTarget target, ModelTransform transform)
        {
            if (!mTransforms.ContainsKey(entityId)) mTransforms[entityId] = new();
            if (!mTransforms[entityId].ContainsKey(target)) mTransforms[entityId][target] = new();
            mTransforms[entityId][target][transformType] = transform;
            mSetTransformSender.SendPacket(entityId, transformType, target, transform);
        }

        public void ResetTransform(long entityId, string transformType, EnumItemRenderTarget target)
        {
            if (!mTransforms.ContainsKey(entityId)) mTransforms[entityId] = new();
            if (!mTransforms[entityId].ContainsKey(target)) mTransforms[entityId][target] = new();
            mTransforms[entityId][target][transformType] = Utils.IdentityTransform();
            mResetTransformSender.SendPacket(entityId, transformType, target, null);
        }

        public ModelTransform GetTransform(long entityId, string transformType, EnumItemRenderTarget target)
        {
            if (!mTransforms.ContainsKey(entityId)) mTransforms[entityId] = new();
            if (!mTransforms[entityId].ContainsKey(target)) mTransforms[entityId][target] = new();
            if (!mTransforms[entityId][target].ContainsKey(transformType)) mTransforms[entityId][target][transformType] = Utils.IdentityTransform();
            return mTransforms[entityId][target][transformType];
        }

        public ModelTransform CalcCurrentTransform(long entityId, EnumItemRenderTarget target)
        {
            currentTransform = Utils.IdentityTransform();
            if (!mTransforms.ContainsKey(entityId) || !mTransforms[entityId].ContainsKey(target)) return currentTransform;
            
            foreach ((string code, ModelTransform transform) in mTransforms[entityId][target])
            {
                currentTransform = Utils.CombineTransforms(currentTransform, transform);
            }

            return currentTransform;
        }

        public void SetEntityId(long entityId, ItemStack item)
        {
            item.Attributes.SetLong(entityIdAttr, entityId);
        }

        public long? GetEntityId(ItemStack item)
        {
            long entityId = item.Attributes.GetLong(entityIdAttr, -1);
            return entityId < 0 ? null : entityId;
        }
    }

    public class TransformPacketSender
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class InputPacket
        {
            public ModelTransform transform;
            public EnumItemRenderTarget target;
            public string transformType;
            public long entityId;
        }

        public delegate void TransformHandler(long entityId, string transformType, EnumItemRenderTarget target, ModelTransform transform);        
        
        private readonly TransformHandler mHandler;
        private readonly ICoreAPI mApi;

        public TransformPacketSender(ICoreAPI api, TransformHandler handler, string channelName)
        {
            mApi = api;

            if (api.Side == EnumAppSide.Client)
            {
                mHandler = handler;
                StartClientSide(api as ICoreClientAPI, channelName);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                StartServerSide(api as ICoreServerAPI, channelName);
            }
        }

        // CLIENT SIDE

        private void StartClientSide(ICoreClientAPI api, string channelName)
        {
            api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>()
            .SetMessageHandler<InputPacket>(OnClientPacket);
        }
        private void OnClientPacket(InputPacket packet)
        {
            mHandler(packet.entityId, packet.transformType, packet.target, packet.transform);
        }

        // SERVER SIDE

        IServerNetworkChannel mServerNetworkChannel;

        private void StartServerSide(ICoreServerAPI api, string channelName)
        {
            mServerNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>();
        }
        public void SendPacket(long entityId, string transformType, EnumItemRenderTarget target, ModelTransform transform)
        {
            if (mServerNetworkChannel == null) return;
            
            InputPacket packet = new InputPacket()
            {
                entityId = entityId,
                transformType = transformType,
                target = target,
                transform = transform
            };

            IServerPlayer sender = ((mApi as ICoreServerAPI)?.World.GetEntityById(entityId) as EntityPlayer)?.Player as IServerPlayer;

            if (sender != null) // @TODO @OPT add optimization for sending transform only no nearby players
            {
                mServerNetworkChannel.BroadcastPacket(packet, sender);
            }
            else
            {
                mServerNetworkChannel.BroadcastPacket(packet);
            }
        }
    }
}
