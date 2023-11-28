using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Systems
{
    public interface IVariantsAnimation
    {
        int StartAnimation(int firstVariant, int lastVariant, ItemSlot slot, EntityAgent player);
        void CancelAnimation(int animationId);
        void SetVariant(int variant, ItemSlot slot, EntityAgent player);
    }

    public interface ISoundSystem
    {
        void PlaySound(string soundCode, ItemSlot slot, EntityAgent player);
    }

    public interface IPlayerAnimationSystem
    {
        void PlayAnimation(string code, ItemSlot slot, EntityAgent player);
    }

    public interface ITranformAnimationSystem
    {
        public class AnimationData
        {
            public string code { get; set; }
            public int? duration { get; set; }
            public ProgressModifiers.ProgressModifier dynamic { get; set; }

            public AnimationData(JsonObject definition)
            {
                code = definition["code"].AsString();
                duration = definition.KeyExists("duration") ? definition["duration"].AsInt(0) : null;
                dynamic = ProgressModifiers.Get(definition["dynamic"].AsString("Linear"));
            }
        }
        
        void PlayAnimation(ItemSlot slot, EntityAgent player, AnimationData animationData, Action finishCallback = null, string mode = "forward");
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

        void PlayAnimation(ItemSlot slot, EntityAgent player, string code, string category, string action = "start");
    }

    public interface IAmmoSelector
    {
        ItemStack GetSelectedAmmo(ItemSlot slot);
        ItemStack TakeSelectedAmmo(ItemSlot slot, int amount = -1);
    }

    public interface IItemStackProvider
    {
        ItemStack GetItemStack(ItemSlot slot, EntityAgent player);
    }

    public interface IAimingSystem
    {
        public struct DirectionOffset
        {
            public float pitch { get; set; } // radian
            public float yaw { get; set; } // radian

            public static implicit operator DirectionOffset((float pitch, float yaw) parameters)
            {
                return new DirectionOffset() { pitch = parameters.pitch, yaw = parameters.yaw };
            }
        }
        DirectionOffset GetShootingDirectionOffset(ItemSlot slot, EntityAgent player);
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