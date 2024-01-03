using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Systems
{
    public interface IVariantsAnimation
    {
        int StartAnimation(int firstVariant, int lastVariant, ItemSlot slot, IPlayer player);
        void CancelAnimation(int animationId);
        void SetVariant(int variant, ItemSlot slot, IPlayer player);
    }

    public interface ISoundSystem
    {
        void PlaySound(string soundCode, IPlayer player);
        void PlaySound(string soundCode, Entity target);
        void StopSound(string soundCode, IPlayer player);
        void StopSound(string soundCode, Entity target);
    }

    public interface IPlayerAnimationSystem
    {
        void PlayAnimation(string code, ItemSlot slot, IPlayer player);
    }

    public interface IAnimationSystem
    {
        public class AnimationData
        {
            public string code { get; set; }
            public float? duration { get; set; }

            public AnimationData(JsonObject definition)
            {
                code = definition["code"].AsString();
                duration = definition.KeyExists("duration") ? definition["duration"].AsFloat() : null;
            }
        }

        void PlayAnimation(ItemSlot slot, IPlayer player, string code, string category, string action = "start", float durationMultiplier = 1);
    }

    public interface IItemStackHolder
    {
        List<ItemStack> Get(ItemSlot slot, IPlayer player);
        List<ItemStack> TakeAll(ItemSlot slot, IPlayer player);
        List<ItemStack> TakeAmount(ItemSlot slot, IPlayer player, int amount);
        List<ItemStack> TakeDurability(ItemSlot slot, IPlayer player, int durability, bool destroy = true, bool overflow = false);
        void Put(ItemSlot slot, IPlayer player, List<ItemStack> items);
        void Clear(ItemSlot slot, IPlayer player);
    }

    public interface IAimingSystem
    {
        Utils.DirectionOffset GetShootingDirectionOffset(ItemSlot slot, IPlayer player);
        TimeSpan GetAimingDuration(ItemSlot slot, IPlayer player);
    }

    static public class ProgressModifiers
    {
        public delegate float ProgressModifier(float progress);
        public readonly static ProgressModifier Linear = (float progress) => { return GameMath.Clamp(progress, 0, 1); };
        public readonly static ProgressModifier Quadratic = (float progress) => { return GameMath.Clamp(progress * progress, 0, 1); };
        public readonly static ProgressModifier Cubic = (float progress) => { return GameMath.Clamp(progress * progress * progress, 0, 1); };
        public readonly static ProgressModifier Sqrt = (float progress) => { return GameMath.Sqrt(GameMath.Clamp(progress, 0, 1)); };
        public readonly static ProgressModifier Sin = (float progress) => { return GameMath.Sin(GameMath.Clamp(progress, 0, 1) * 2 / GameMath.PI); };
        public readonly static ProgressModifier SinQuadratic = (float progress) => { return GameMath.Sin(GameMath.Clamp(progress * progress, 0, 1) * 2 / GameMath.PI); };

        public static ProgressModifier Get(string name)
        {
            switch (name)
            {
                case "Linear": return Linear;
                case "Quadratic": return Quadratic;
                case "Cubic": return Cubic;
                case "Sqrt": return Sqrt;
                case "Sin": return Sin;
                case "SinQuadratic": return SinQuadratic;
                default: throw new NotImplementedException();
            }
        }
    }
}