using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;

namespace MaltiezFSM.Systems
{
    internal class NoSway : BaseSystem
    {
        private ActionCallbackTimer mTimer;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mTimer = new();
            mTimer.Init(api, SetAimAnimationCrutch);
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    if (mApi is ICoreClientAPI) mTimer.Start();
                    break;
                case "stop":
                    if (mApi is ICoreClientAPI) mTimer.Stop();
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [NoSway] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }

        void SetAimAnimationCrutch(bool interact)
        {
            EntityPlayer player = ((mApi as ICoreClientAPI)?.World as ClientMain)?.EntityPlayer;

            if (player?.Controls?.HandUse == null) return;

            if (interact)
            {
                player.AnimManager.StopAnimation("placeblock");
                player.Controls.HandUse = EnumHandInteract.HeldItemInteract;
            }
            else
            {
                player.Controls.HandUse = EnumHandInteract.None;
            }
        }
    }

    internal sealed class ActionCallbackTimer
    {
        private Action<bool> mCallback;
        private ICoreAPI mApi;
        private long? mCallbackId;

        public void Init(ICoreAPI api, Action<bool> callback)
        {
            mCallback = callback;
            mApi = api;
        }
        public void Start()
        {
            mCallback(true);
            SetListener();
        }
        public void Handler(float time)
        {
            mCallback(true);
            SetListener();
        }
        public void Stop()
        {
            StopListener();
            mCallback(false);
        }

        private void SetListener()
        {
            StopListener();
            mCallbackId = mApi.World.RegisterGameTickListener(Handler, 0);
        }
        private void StopListener()
        {
            if (mCallbackId != null) mApi.World.UnregisterGameTickListener((long)mCallbackId);
            mCallbackId = null;
        }
    }
}
