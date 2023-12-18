using MaltiezFSM.API;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace MaltiezFSM.Framework
{
    public class CustomInputManager : ICustomInputManager
    {
        private readonly List<ICustomInput> mInputs = new();
        private readonly List<ICustomInputManager.InputCallback> mCallbacks = new();
        private readonly Dictionary<ICustomInput, int> mInputsIndexes = new();
        private readonly CustomInputPacketSender mSender;
        private readonly bool mServerSide;

        public CustomInputManager(ICoreAPI api)
        {
            mSender = new CustomInputPacketSender(api, InvokeCustomInput, "FSMlib.custominputmanager");
            mServerSide = api.Side == EnumAppSide.Server;
        }

        void ICustomInputManager.InvokeCustomInput(ICustomInput input, long playerEntityId)
        {
            int inputIndex = mInputsIndexes[input];

            if (mServerSide)
            {
                mSender.SendPacket(inputIndex, playerEntityId);
            }
            else
            {
                mCallbacks[inputIndex](input);
            }
        }

        void ICustomInputManager.RegisterCustomInput(ICustomInput input, ICustomInputManager.InputCallback callback)
        {
            int inputIndex = mInputs.Count;
            mInputs.Add(input);
            mCallbacks.Add(callback);
            mInputsIndexes.Add(input, inputIndex);
        }

        private void InvokeCustomInput(int inputIndex)
        {
            mCallbacks[inputIndex](mInputs[inputIndex]);
        }
    }

    public class CustomInputPacketSender
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class InputPacket
        {
            public int inputIndex;
        }

        public delegate void InputHandler(int inputIndex);

        private readonly InputHandler mHandler;
        private readonly ICoreAPI mApi;

        public CustomInputPacketSender(ICoreAPI api, InputHandler handler, string channelName)
        {
            mApi = api;
            mHandler = handler;

            if (api.Side == EnumAppSide.Client)
            {
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
            mHandler(packet.inputIndex);
        }

        // SERVER SIDE

        IServerNetworkChannel mServerNetworkChannel;

        private void StartServerSide(ICoreServerAPI api, string channelName)
        {
            mServerNetworkChannel = api.Network.RegisterChannel(channelName)
            .RegisterMessageType<InputPacket>();
        }
        public void SendPacket(int inputIndex, long entityId)
        {
            InputPacket packet = new InputPacket()
            {
                inputIndex = inputIndex
            };

            IServerPlayer receiver = ((mApi as ICoreServerAPI)?.World.GetEntityById(entityId) as EntityPlayer)?.Player as IServerPlayer;

            if (receiver != null)
            {
                mServerNetworkChannel.SendPacket(packet, receiver);
            }
        }
    }
}
