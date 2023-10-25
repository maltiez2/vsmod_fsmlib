﻿using Vintagestory.API.Common;
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
        void PlayAnimation(string soundCode, ItemSlot slot, EntityAgent player);
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
}