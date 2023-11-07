using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using MaltiezFSM.API;
using System.Collections.Generic;
using System;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;

namespace MaltiezFSM.Systems
{
    public class BasicVariantsAnimation<TAnimationPlayer> : BaseSystem
        where TAnimationPlayer : IAnimationPlayer, new()
    {
        public const string animationsAttrName = "animations";
        public const string firstVariantAttrName = "firstVariant";
        public const string lastVariantAttrName = "lastVariant";
        public const string durationAttrName = "duration";
        public const string codeAttrName = "code";
        public const string soundSystemAttrName = "soundSystem";
        public const string soundsAttrName = "sounds";
        public const string variantAttrName = "variant";
        public const string descriptionAttrName = "description";

        private readonly Dictionary<string, IAnimationPlayer.AnimationParameters> mAnimations = new();
        private readonly Dictionary<string, Dictionary<int, string>> mAnimationsSounds = new();
        private readonly Dictionary<string, string> mDescriptions = new();
        private Dictionary<long, TAnimationPlayer> mTimers = new();
        private ISoundSystem mSoundSystem;
        private string mSoundSystemId = "";

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            JsonObject[] animations = definition[animationsAttrName].AsArray();
            foreach (JsonObject animation in animations)
            {
                string animationCode = animation[codeAttrName].AsString();

                mAnimations.Add(
                    animationCode,
                    (
                        animation[firstVariantAttrName].AsInt(),
                        animation[lastVariantAttrName].AsInt(),
                        animation[durationAttrName].AsInt()
                    )
                );

                mAnimationsSounds.Add(animationCode, new());

                if (animation.KeyExists(soundsAttrName))
                {
                    foreach (JsonObject soundDefinition in animation[soundsAttrName].AsArray())
                    {
                        mAnimationsSounds[animationCode].Add(soundDefinition[variantAttrName].AsInt(), soundDefinition[codeAttrName].AsString());
                    }
                }

                mDescriptions.Add(animationCode, animation[descriptionAttrName].AsString());
            }

            if (definition.KeyExists(soundSystemAttrName))
            {
                mSoundSystemId = definition[soundSystemAttrName].AsString();
            }
        }
        public override void SetSystems(Dictionary<string, ISystem> systems)
        {
            if (systems.ContainsKey(mSoundSystemId)) mSoundSystem = systems[mSoundSystemId] as ISoundSystem;
        }
        public override bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Verify(slot, player, parameters)) return false;
            if (parameters["progressive"].AsBool()) return true;
            
            string code = parameters[codeAttrName].AsString();
            if (code == null) mApi.Logger.Error("[FSMlib] [BasicVariantsAnimation] [Verify] No code received");
            if (!mAnimations.ContainsKey(code)) mApi.Logger.Error("[FSMlib] [BasicVariantsAnimation] [Verify] No animations with code '" + code + "' are defined");

            return mAnimations.ContainsKey(code);
        }
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            if (parameters["progressive"].AsBool())
            {
                int renderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 1);
                int length = parameters["length"].AsInt(1);
                int limit = parameters["limit"].AsInt(1);
                renderVariant += length;
                if (renderVariant < 1) renderVariant = 1;
                if (renderVariant > limit) renderVariant = limit;
                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
                slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);
                (player as EntityPlayer)?.Player.InventoryManager.BroadcastHotbarSlot();
                slot.MarkDirty();
                return true;
            }

            string code = parameters[codeAttrName].AsString();

            if (code == null) return false;
            if (!mAnimations.ContainsKey(code)) return false;

            IAnimationPlayer.AnimationParameters animation = mAnimations[code];

            if (!mTimers.ContainsKey(player.EntityId)) mTimers[player.EntityId] = new();

            mTimers[player.EntityId]?.Stop();
            mTimers[player.EntityId] = new TAnimationPlayer();
            mTimers[player.EntityId].Init(mApi, animation, (int variant) => SetRenderVariant(variant, slot, player, mAnimationsSounds[code]));
            mTimers[player.EntityId].Play();

            return true;
        }
        public override string[] GetDescription(ItemSlot slot, IWorldAccessor world)
        {
            List<string> output = new();

            foreach (var entry in mAnimations)
            {
                string descriptionTemplate = mDescriptions[entry.Key];
                if (descriptionTemplate == null) continue;

                output.Add(Lang.Get(descriptionTemplate, (float)entry.Value.duration_ms / 1000));
            }

            return output.ToArray();
        }

        private void SetRenderVariant(int renderVariant, ItemSlot weaponSlot, EntityAgent byEntity, Dictionary<int, string> sounds)
        {
            if (weaponSlot?.Itemstack == null) return;

            if (sounds.ContainsKey(renderVariant)) mSoundSystem?.PlaySound(sounds[renderVariant], weaponSlot, byEntity);

            int prevRenderVariant = weaponSlot.Itemstack.Attributes.GetInt("renderVariant", 0);

            weaponSlot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
            weaponSlot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

            if (prevRenderVariant == renderVariant) return;

            (byEntity as EntityPlayer)?.Player.InventoryManager.BroadcastHotbarSlot();
        }
    }

    public interface IAnimationPlayer
    {
        public struct AnimationParameters
        {
            public int firstVariant { get; set; }
            public int lastVariant { get; set; }
            public int duration_ms { get; set; }

            public static implicit operator AnimationParameters((int first, int last, int duration) parameters)
            {
                return new AnimationParameters() { firstVariant = parameters.first, lastVariant = parameters.last, duration_ms = parameters.duration };
            }
        }

        void Init(ICoreAPI api, AnimationParameters parameters, Action<int> callback);
        void Play();
        void Stop();
        void Revert();
    }

    public class TimerBasedAnimation : IAnimationPlayer
    {
        private Action<int> mCallback;
        private ICoreAPI mApi;
        private long? mCallbackId;

        private int mFirstVariant;
        private int mNextVariant;
        private int mLastVariant;
        private int mDelay_ms;

        public void Init(ICoreAPI api, IAnimationPlayer.AnimationParameters parameters, Action<int> callback)
        {
            mCallback = callback;
            mApi = api;
            mFirstVariant = parameters.firstVariant;
            mLastVariant = parameters.lastVariant;
            mNextVariant = parameters.firstVariant;
            mDelay_ms = (mLastVariant - mNextVariant + 1 == 0) ? parameters.duration_ms : parameters.duration_ms / (mLastVariant - mNextVariant + 1);
            
        }
        public void Play()
        {
            mCallback(NextVariant());
            if (mNextVariant > mLastVariant || mNextVariant < mFirstVariant) return;
            mCallbackId = mApi.World.RegisterCallback(Handler, mDelay_ms);
        }
        public void Handler(float time)
        {
            mCallback(NextVariant());

            if (mNextVariant > mLastVariant || mNextVariant < mFirstVariant) return;

            int newDelay = 2 * mDelay_ms - (int)(time * 1000);
            mCallbackId = mApi.World.RegisterCallback(Handler, newDelay);
        }
        public void Stop()
        {
            if (mCallbackId == null) return;

            mApi.World.UnregisterCallback((long)mCallbackId);
            mCallbackId = null;
        }
        public void Revert()
        {
            Stop();
            mCallback(mFirstVariant);
        }
        private int NextVariant()
        {
            if (mFirstVariant <= mLastVariant)
            {
                return mNextVariant++;
            }
            else
            {
                return mNextVariant--;
            }
        }
    }

    public sealed class TickBasedAnimation : IAnimationPlayer
    {
        private Action<int> mCallback;
        private ICoreAPI mApi;
        private long? mCallbackId;

        private int mFirstVariant;
        private int mNextVariant;
        private int mLastVariant;
        private int mDelay_ms;
        private int mTimeElapsed_ms;

        public void Init(ICoreAPI api, IAnimationPlayer.AnimationParameters parameters, Action<int> callback)
        {
            mCallback = callback;
            mApi = api;
            mFirstVariant = parameters.firstVariant;
            mLastVariant = parameters.lastVariant;
            mNextVariant = parameters.firstVariant;
            int variantsNumber = mLastVariant > mNextVariant ? mLastVariant - mNextVariant : mNextVariant - mLastVariant;
            mDelay_ms = (variantsNumber == 0) ? parameters.duration_ms : parameters.duration_ms / variantsNumber;
        }
        public void Play()
        {
            if (!SetVariant()) return;
            mTimeElapsed_ms = 0;
            SetListener();
        }
        public void Handler(float time)
        {
            if (!CheckTime(time)) return;
            if (!SetVariant()) return;

            SetListener();
        }
        public void Stop()
        {
            StopListener();
        }
        public void Revert()
        {
            RevertVariant();
        }

        private bool CheckTime(float time)
        {
            mTimeElapsed_ms += (int)(time * 1000);
            if (mTimeElapsed_ms < mDelay_ms) return false;
            mTimeElapsed_ms -= mDelay_ms;
            return true;
        }
        private bool SetVariant()
        {
            if ((mNextVariant - mLastVariant) * (mNextVariant - mFirstVariant) > 0)
            {
                StopListener();
                return false;
            }
            mCallback(mNextVariant);
            if (mFirstVariant <= mLastVariant)
            {
                mNextVariant++;
            }
            else
            {
                mNextVariant--;
            }
            return true;
        }
        private void RevertVariant()
        {
            mCallback(mFirstVariant);
        }
        private void SetListener()
        {
            StopListener();
            if ((mNextVariant - mLastVariant) * (mNextVariant - mFirstVariant) > 0) return;
            mCallbackId = mApi.World.RegisterGameTickListener(Handler, 0);
        }
        private void StopListener()
        {
            if (mCallbackId != null) mApi.World.UnregisterGameTickListener((long)mCallbackId);
            mCallbackId = null;
        }
    }
}
