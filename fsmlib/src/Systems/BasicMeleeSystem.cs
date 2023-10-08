using MaltiezFirearms.FiniteStateMachine.API;
using MaltiezFirearms.FiniteStateMachine.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFirearms.FiniteStateMachine.Systems
{
    internal class BasicMelee : UniqueIdFactoryObject, ISystem // @TODO Placeholder system
    {
        private MeleeCallbackTimer mTimer;
        private ICoreAPI mApi;
        private CollectibleObject mCollectible;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mApi = api;
            mTimer = new();
            mCollectible = collectible;
        }

        void ISystem.SetSystems(Dictionary<string, ISystem> systems)
        {
        }

        bool ISystem.Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            string action = parameters["action"].AsString();
            switch (action)
            {
                case "start":
                    if (mApi is ICoreClientAPI)
                    {
                        mTimer.Init(mApi, (float time) => TryAttack(time, player));
                        mTimer.Start();
                    }
                    break;
                case "stop":
                    if (mApi is ICoreClientAPI) mTimer.Stop();
                    mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = null;
                    mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = null;
                    break;
            }
            return true;
        }

        bool ISystem.Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            return true;
        }

        public bool TryAttack(float secondsPassed, EntityAgent byEntity)
        {
            var entitySel = (byEntity as EntityPlayer).EntitySelection;

            if (secondsPassed == 0)
            {
                byEntity.Attributes.SetInt("didattack", 0);
                byEntity.World.RegisterCallback(delegate
                {
                    IPlayer player = (byEntity as EntityPlayer).Player;
                    if (player != null && byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                    {
                        float pitchModifier = (byEntity as EntityPlayer).talkUtil.pitchModifier;
                        player.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), player.Entity, player, pitchModifier * 0.9f + (float)mApi.World.Rand.NextDouble() * 0.2f, 16f, 0.35f);
                    }
                }, 464);
            }

            float backwards = 0f - Math.Min(0.8f, 3f * secondsPassed);
            float stab = Math.Min(1.2f, 20f * Math.Max(0f, secondsPassed - 0.25f));
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor clientWorldAccessor = byEntity.World as IClientWorldAccessor;
                ModelTransform modelTransform = new ModelTransform();
                modelTransform.EnsureDefaultValues();
                float animationProgressSum = stab + backwards;
                float animationProgressYAxis = Math.Min(0.2f, 1.5f * secondsPassed);
                float easeOut = Math.Max(0f, 2f * (secondsPassed - 1f));
                if (secondsPassed > 0.4f)
                {
                    animationProgressSum = Math.Max(0f, animationProgressSum - easeOut);
                }

                animationProgressYAxis = Math.Max(0f, animationProgressYAxis - easeOut);

                if (mCollectible.Code.Path.Contains("bayonet")) // Just placeholder
                {
                    modelTransform.Translation.Set(-1f * animationProgressSum + 0.2f, animationProgressYAxis * 0.4f, (0f - animationProgressSum) * 0.8f * 2.6f);
                    modelTransform.Rotation.Set((0.3f - animationProgressSum) * 9f, animationProgressSum * 5f, (0f - animationProgressSum) * 30f);
                }
                else
                {
                    modelTransform.Translation.Set(0f * (-0.5f + animationProgressSum), 1f + animationProgressYAxis * -3.0f - 0.5f, (-1.0f - animationProgressSum) * 0.8f * 2.6f);
                    modelTransform.Rotation.Set((-20.0f - animationProgressSum) * 9f, animationProgressSum * 5f - 10f, (0f - animationProgressSum) * 30f);
                }

                byEntity.Controls.UsingHeldItemTransformAfter = modelTransform;
                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    clientWorldAccessor.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    clientWorldAccessor.AddCameraShake(0.25f);
                }

                mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = modelTransform;
                mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = modelTransform;
            }

            if (secondsPassed >= 1.2f)
            {
                mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = null;
                mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = null;
            }

            return secondsPassed < 1.2f;
        }
    }

    internal sealed class MeleeCallbackTimer
    {
        private System.Func<float, bool> mCallback;
        private ICoreAPI mApi;
        private long? mCallbackId;
        private float mTime;

        public void Init(ICoreAPI api, System.Func<float, bool> callback)
        {
            mCallback = callback;
            mApi = api;
        }
        public void Start()
        {
            mCallback(0);
            mTime = 0;
            SetListener();
        }
        public void Handler(float time)
        {
            mTime += time;
            if (mCallback(mTime))
            {
                SetListener();
            }
            else
            {
                StopListener();
            }
        }
        public void Stop()
        {
            StopListener();
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
