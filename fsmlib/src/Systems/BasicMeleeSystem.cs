using MaltiezFSM.Framework;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

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
            bool flipTpAxles = definition["flipTpAxles"].AsBool(false);
            windUp = (float)definition["windUp_ms"].AsInt() / 1000;
            strike = (float)definition["strike_ms"].AsInt() / 1000;
            easeOff = (float)definition["easeOff_ms"].AsInt() / 1000;
            fpWindUp = GetTransform(definition["fpWindUp"], false);
            tpWindUp = GetTransform(definition["tpWindUp"], flipTpAxles);
            fpStrike = GetTransform(definition["fpStrike"], false);
            tpStrike = GetTransform(definition["tpStrike"], flipTpAxles);
        }

        private ModelTransform GetTransform(JsonObject transform, bool flipAxles)
        {
            JsonObject translation = transform["translation"];
            JsonObject rotation = transform["rotation"];
            JsonObject origin = transform["origin"];
            

            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();
            if (!flipAxles)
            {
                modelTransform.Translation.Set(translation["x"].AsFloat(), translation["y"].AsFloat(), translation["z"].AsFloat());
                modelTransform.Rotation.Set(rotation["x"].AsFloat(), rotation["y"].AsFloat(), rotation["z"].AsFloat());
                modelTransform.Origin.Set(origin["x"].AsFloat(), origin["y"].AsFloat(), origin["z"].AsFloat());
            }
            else
            {
                modelTransform.Translation.Set(translation["z"].AsFloat(), translation["y"].AsFloat(), -translation["x"].AsFloat());
                modelTransform.Rotation.Set(rotation["x"].AsFloat(), rotation["y"].AsFloat(), rotation["z"].AsFloat());
                modelTransform.Origin.Set(origin["z"].AsFloat(), origin["y"].AsFloat(), -origin["x"].AsFloat());
            }
            modelTransform.Scale = transform["scale"].AsFloat(1);
            return modelTransform;
        }
    }
    
    public class BasicMelee : BaseSystem
    {
        private MeleeCallbackTimer mTimer;
        private AnimationData mAnimation;
        private ModelTransform mFpInitial;
        private ModelTransform mTpInitial;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mTimer = new();
            mAnimation = new AnimationData(definition);                                                                 
        }

        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

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

                        mTimer.Init(mApi, (float time) => TryAttack(time, slot, player));
                        mTimer.Start();
                    }
                    break;
                case "stop":
                    if (mApi is ICoreClientAPI) mTimer.Stop();
                    mCollectible.GetBehavior<FiniteStateMachineBehaviour>().tpTransform = null;
                    mCollectible.GetBehavior<FiniteStateMachineBehaviour>().fpTransform = null;
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicMelee] [Process] Action does not exists: " + action);
                    return false;
            }
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

        private bool TryAttack(float secondsPassed, ItemSlot slot, EntityAgent byEntity)
        {
            if (secondsPassed > mAnimation.windUp + mAnimation.strike + mAnimation.easeOff) return false;

            PlayAnimation(secondsPassed);

            if (mAnimation.windUp < secondsPassed && secondsPassed < mAnimation.windUp + mAnimation.strike && byEntity.Attributes.GetInt("didattack") == 0)
            {
                EntitySelection entitySel = (byEntity as EntityPlayer)?.EntitySelection;
                (byEntity.World as IClientWorldAccessor)?.TryAttackEntity(entitySel);
                (byEntity.World as IClientWorldAccessor)?.AddCameraShake(0.1f);
                if ((byEntity as EntityPlayer)?.EntitySelection != null) slot?.Itemstack?.Item?.DamageItem(byEntity.World, byEntity, slot);
                slot?.MarkDirty();
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
