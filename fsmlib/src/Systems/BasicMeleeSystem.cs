using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace MaltiezFSM.Systems
{
    public class AnimationData
    {
        public float windUp { get; set; }
        public float strike { get; set; }
        public float easeOff { get; set; }
        public ModelTransform fpWindUp { get; set; }
        public ModelTransform tpWindUp { get; set; }
        public ModelTransform fpStrike {  get; set; }
        public ModelTransform tpStrike { get; set; }

        public AnimationData(JsonObject definition)
        {
            windUp = (float)definition["windUp_ms"].AsInt() / 1000;
            strike = (float)definition["strike_ms"].AsInt() / 1000;
            easeOff = (float)definition["easeOff_ms"].AsInt() / 1000;
            fpWindUp = GetTransform(definition["fpWindUp"]);
            tpWindUp = GetTransform(definition["tpWindUp"]);
            fpStrike = GetTransform(definition["fpStrike"]);
            tpStrike = GetTransform(definition["tpStrike"]);
        }

        private ModelTransform GetTransform(JsonObject transform)
        {
            JsonObject translation = transform["translation"];
            JsonObject rotation = transform["rotation"];
            JsonObject origin = transform["origin"];

            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();
            modelTransform.Translation.Set(translation["x"].AsFloat(), translation["y"].AsFloat(), translation["z"].AsFloat());
            modelTransform.Rotation.Set(rotation["x"].AsFloat(), rotation["y"].AsFloat(), rotation["z"].AsFloat());
            modelTransform.Origin.Set(origin["x"].AsFloat(), origin["y"].AsFloat(), origin["z"].AsFloat());
            modelTransform.Scale = transform["scale"].AsFloat(1);
            return modelTransform;
        }
    }
    
    public class BasicMelee : UniqueIdFactoryObject, ISystem // @TODO Placeholder system
    {
        private MeleeCallbackTimer mTimer;
        private ICoreAPI mApi;
        private CollectibleObject mCollectible;
        private AnimationData mAnimation;
        private ModelTransform mFpInitial;
        private ModelTransform mTpInitial;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mApi = api;
            mTimer = new();
            mCollectible = collectible;
            mAnimation = new AnimationData(definition);                                                                      
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
                        mFpInitial = mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform?.Clone();
                        mTpInitial = mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform?.Clone();

                        if (mFpInitial == null) mFpInitial = GetIdentityTransform();
                        if (mTpInitial == null) mTpInitial = GetIdentityTransform();

                        player.Attributes.SetInt("didattack", 0);

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

        private ModelTransform GetIdentityTransform()
        {
            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();
            modelTransform.Translation.Set(0, 0, 0);
            modelTransform.Rotation.Set(0, 0, 0);
            modelTransform.Origin.Set(0, 0, 0);
            modelTransform.Scale = 1;
            return modelTransform;
        }

        private bool TryAttack(float secondsPassed, EntityAgent byEntity)
        {
            if (secondsPassed > mAnimation.windUp + mAnimation.strike + mAnimation.easeOff) return false;

            PlayAnimation(secondsPassed);

            if (mAnimation.windUp < secondsPassed && secondsPassed < mAnimation.windUp + mAnimation.strike && byEntity.Attributes.GetInt("didattack") == 0)
            {
                EntitySelection entitySel = (byEntity as EntityPlayer)?.EntitySelection;
                (byEntity.World as IClientWorldAccessor)?.TryAttackEntity(entitySel);
                (byEntity.World as IClientWorldAccessor)?.AddCameraShake(0.25f);
                byEntity.Attributes.SetInt("didattack", 1);
            }

            return true;
        }

        private bool PlayAnimation(float secondsPassed)
        {
            if (secondsPassed < mAnimation.windUp)
            {
                PlayWindUp(secondsPassed / mAnimation.windUp);
            }
            else if (secondsPassed < mAnimation.strike + mAnimation.windUp)
            {
                PlayStrike((secondsPassed - mAnimation.strike) / mAnimation.windUp);
            }
            else if (secondsPassed < mAnimation.strike + mAnimation.windUp + mAnimation.easeOff)
            {
                PlayEaseOff((secondsPassed - mAnimation.strike - mAnimation.windUp) / mAnimation.easeOff);
            }
            else
            {
                return false;
            }

            return true;
        }

        private void PlayWindUp(float progress)
        {
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = ApplyProgress(MathF.Sqrt(progress), mFpInitial, mAnimation.fpWindUp);
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = ApplyProgress(MathF.Sqrt(progress), mTpInitial, mAnimation.tpWindUp);
        }

        private void PlayStrike(float progress)
        {
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = ApplyProgress(progress * progress, mAnimation.fpWindUp, mAnimation.fpStrike);
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = ApplyProgress(progress * progress, mAnimation.tpWindUp, mAnimation.tpStrike);
        }

        private void PlayEaseOff(float progress)
        {
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = ApplyProgress(progress, mAnimation.fpStrike, mFpInitial);
            mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = ApplyProgress(progress, mAnimation.tpStrike, mTpInitial);
        }

        private ModelTransform ApplyProgress(float progress, ModelTransform startTransofrm, ModelTransform endTransform)
        {
            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();
            modelTransform.Translation = startTransofrm.Translation + (endTransform.Translation - startTransofrm.Translation) * progress;
            modelTransform.Rotation = startTransofrm.Rotation + (endTransform.Rotation - startTransofrm.Rotation) * progress;
            modelTransform.Origin = startTransofrm.Origin + (endTransform.Origin - startTransofrm.Origin) * progress;
            modelTransform.Scale = startTransofrm.ScaleXYZ.X + (endTransform.ScaleXYZ.X - startTransofrm.ScaleXYZ.X) * progress;
            return modelTransform;
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
