using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using System;
using Vintagestory.API.Util;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.Server;
using HarmonyLib;
using System.Linq;

namespace MaltiezFSM.Framework
{
    public static class Utils
    {
        static public ModelTransform ToTransformFrom(JsonObject transform, float multiplier = 1)
        {
            JsonObject translation = transform["translation"];
            JsonObject rotation = transform["rotation"];
            JsonObject origin = transform["origin"];

            ModelTransform modelTransform = IdentityTransform();
            modelTransform.Translation.Set(multiplier * translation["x"].AsFloat(), multiplier * translation["y"].AsFloat(), multiplier * translation["z"].AsFloat());
            modelTransform.Rotation.Set(multiplier * rotation["x"].AsFloat(), multiplier * rotation["y"].AsFloat(), multiplier * rotation["z"].AsFloat());
            modelTransform.Origin.Set(multiplier * origin["x"].AsFloat(), multiplier * origin["y"].AsFloat(), multiplier * origin["z"].AsFloat());
            modelTransform.Scale = transform["scale"].AsFloat(1);
            return modelTransform;
        }
        static public ModelTransform CombineTransforms(ModelTransform first, ModelTransform second, float secondMultiple = 1)
        {
            ModelTransform output = first.Clone();
            SumVectors(output.Translation, first.Translation, second.Translation, secondMultiple);
            SumVectors(output.Rotation, first.Rotation, second.Rotation, secondMultiple);
            SumVectors(output.Origin, first.Origin, second.Origin, secondMultiple);
            MultVectorsByComponent(output.ScaleXYZ, first.ScaleXYZ, second.ScaleXYZ);
            return output;
        }
        static public ModelTransform SubtractTransformsNoScale(ModelTransform first, ModelTransform second)
        {
            ModelTransform output = first.Clone();
            SumVectors(output.Translation, first.Translation, second.Translation, -1);
            SumVectors(output.Rotation, first.Rotation, second.Rotation, -1);
            SumVectors(output.Origin, first.Origin, second.Origin, -1);
            return output;
        }
        static public ModelTransform TransitionTransform(ModelTransform fromTransform, ModelTransform toTransform, float progress)
        {
            ModelTransform output = toTransform.Clone();
            output.Translation = TransitionVector(fromTransform.Translation, toTransform.Translation, progress);
            output.Rotation = TransitionVector(fromTransform.Rotation, toTransform.Rotation, progress);
            output.Origin = TransitionVector(fromTransform.Origin, toTransform.Origin, progress);
            output.ScaleXYZ = TransitionVector(fromTransform.ScaleXYZ, toTransform.ScaleXYZ, progress);
            return output;
        }
        static public ModelTransform IdentityTransform()
        {
            ModelTransform output = new();
            output.Translation = new(0, 0, 0);
            output.Rotation = new(0, 0, 0);
            output.Origin = new(0, 0, 0);
            output.ScaleXYZ = new(1, 1, 1);
            return output;
        }

        static public Vec3f TransitionVector(Vec3f from, Vec3f to, float progress)
        {
            return from + (to - from) * progress;
        }
        static public void SumVectors(Vec3f output, Vec3f first, Vec3f second, float secondMultiple = 1)
        {
            output.X = first.X + second.X * secondMultiple;
            output.Y = first.Y + second.Y * secondMultiple;
            output.Z = first.Z + second.Z * secondMultiple;
        }
        static public void MultVectorsByComponent(Vec3f output, Vec3f first, Vec3f second)
        {
            output.X = first.X * second.X;
            output.Y = first.Y * second.Y;
            output.Z = first.Z * second.Z;
        }

        static public Vec3f FromCameraReferenceFrame(EntityAgent player, Vec3f position)
        {
            Vec3f viewVector = player.SidedPos.GetViewVector();
            Vec3f vertical = new Vec3f(0, 1, 0);
            Vec3f localZ = viewVector.Normalize();
            Vec3f localX = viewVector.Cross(vertical).Normalize();
            Vec3f localY = localX.Cross(localZ);
            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public Vec3d FromCameraReferenceFrame(EntityAgent player, Vec3d position)
        {
            Vec3f viewVectorF = player.SidedPos.GetViewVector();
            Vec3d viewVector = new Vec3d(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
            Vec3d vertical = new Vec3d(0, 1, 0);
            Vec3d localZ = viewVector.Normalize();
            Vec3d localX = viewVector.Cross(vertical).Normalize();
            Vec3d localY = localX.Cross(localZ);
            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public Vec3d ToCameraReferenceFrame(EntityAgent player, Vec3d position)
        {
            Vec3f viewVectorF = player.SidedPos.GetViewVector();
            Vec3d viewVector = new Vec3d(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
            Vec3d vertical = new Vec3d(0, 1, 0);
            Vec3d localZ = viewVector.Normalize();
            Vec3d localX = viewVector.Cross(vertical).Normalize();
            Vec3d localY = localX.Cross(localZ);

            InverseMatrix(localX, localY, localZ);

            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public void InverseMatrix(Vec3d X, Vec3d Y, Vec3d Z)
        {
            double[] matrix = { X.X, X.Y, X.Z, Y.X, Y.Y, Y.Z, Z.X, Z.Y, Z.Z };
            Mat3d.Invert(matrix, matrix);
            X.X = matrix[0];
            X.Y = matrix[1];
            X.Z = matrix[2];
            Y.X = matrix[3];
            Y.Y = matrix[4];
            Y.Z = matrix[5];
            Z.X = matrix[6];
            Z.Y = matrix[7];
            Z.Z = matrix[8];
        }


        // *** Based on code from Dana (https://github.com/Craluminum2413)
        public static void SetArray<TArray>(this ITreeAttribute tree, string key, TArray data) 
        {
            tree.SetBytes(key, SerializerUtil.Serialize(data));
        }
        public static TArray GetArray<TArray>(this ITreeAttribute tree, string key, TArray defaultValue = default)
        {
            byte[] array = tree.GetBytes(key);
            if (array == null)
            {
                return defaultValue;
            }
            return SerializerUtil.Deserialize<TArray>(array);
        }
        // ***

        public static AssetLocation[] GetAssetLocations(JsonObject definition)
        {
            if (definition.IsArray())
            {
                List<AssetLocation> locations = new();
                foreach (JsonObject location in definition.AsArray())
                {
                    locations.Add(new(location.AsString()));
                }
                return locations.ToArray();
            }
            else
            {
                return new AssetLocation[] { new AssetLocation(definition.AsString()) };
            }
        }
        
        public enum RequirementSlotType
        {
            mainhand,
            offhand,
            inventory
        }

        public enum RequirementSearchMode
        {
            whitelist,
            blacklist
        }

        public enum RequirementItemProcessMode
        {
            none,
            durabilityChange,
            durabilityDamage,
            consumeAmount
        }

        public class ItemRequirement
        {
            public RequirementSlotType slot { get; set; }
            public RequirementSearchMode mode { get; set; }
            public RequirementItemProcessMode process {  get; set; }
            public AssetLocation[] locations { get; set; }
            public int processParameter { get; set; }

            public ItemRequirement(JsonObject definition)
            {
                slot = (RequirementSlotType)Enum.Parse(typeof(RequirementSlotType), definition["slot"].AsString("inventory"));
                mode = (RequirementSearchMode)Enum.Parse(typeof(RequirementSearchMode), definition["mode"].AsString("whitelist"));
                process = (RequirementItemProcessMode)Enum.Parse(typeof(RequirementItemProcessMode), definition["process"].AsString("none"));
                locations = definition.KeyExists("location") ? GetAssetLocations(definition["location"]) : Array.Empty<AssetLocation>();
                processParameter = definition["amount"].AsInt(0);
            }

            public ItemSlot GetSlot(EntityAgent entity)
            {
                ItemSlot itemSlot = null;
                switch (slot)
                {
                    case RequirementSlotType.mainhand:
                        itemSlot = entity.RightHandItemSlot;
                        break;
                    case RequirementSlotType.offhand:
                        itemSlot = entity.LeftHandItemSlot;
                        break;
                    case RequirementSlotType.inventory:
                        List<ItemSlot> slots = CheckInventory(entity, false);
                        if (slots.Count == 0)
                        {
                            return null;
                        }
                        else
                        {
                            return slots[0];
                        }
                }

                if (!CheckSlot(itemSlot)) return null;

                return itemSlot;
            }

            public List<ItemSlot> GetSlots(EntityAgent entity)
            {
                List<ItemSlot> itemSlots = new();
                switch (slot)
                {
                    case RequirementSlotType.mainhand:
                        if (!CheckSlot(entity.RightHandItemSlot)) itemSlots.Add(entity.RightHandItemSlot);
                        break;
                    case RequirementSlotType.offhand:
                        if (!CheckSlot(entity.LeftHandItemSlot)) itemSlots.Add(entity.LeftHandItemSlot);
                        break;
                    case RequirementSlotType.inventory:
                        List<ItemSlot> slots = CheckInventory(entity);
                        return slots;
                }

                return itemSlots;
            }

            public void Process(List<ItemSlot> slots, EntityAgent byEntity)
            {
                foreach (ItemSlot slot in slots)
                {
                    Process(slot, byEntity);
                }
            }

            public void Process(ItemSlot slot, EntityAgent byEntity)
            {
                switch (process)
                {
                    case RequirementItemProcessMode.none:
                        break;
                    case RequirementItemProcessMode.durabilityChange:
                        ChangeDurability(slot.Itemstack, processParameter);
                        break;
                    case RequirementItemProcessMode.durabilityDamage:
                        if (processParameter > 0)
                        {
                            slot.Itemstack.Item.DamageItem(byEntity.World, byEntity, slot, processParameter);
                        }
                        else if (processParameter < 0)
                        {
                            int currentDurability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
                            int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
                            int newDurability = Math.Clamp(currentDurability - processParameter, 0, maxDurability);
                            slot.Itemstack.Attributes.SetInt("durability", newDurability);
                        }
                        break;
                    case RequirementItemProcessMode.consumeAmount:
                        slot.TakeOut(processParameter);
                        break;
                }
            }

            public bool CheckSlot(ItemSlot slot)
            {
                CollectibleObject collectible = slot?.Itemstack?.Collectible;

                bool match = collectible?.WildCardMatch(locations) == true;
                
                if (match)
                {
                    switch (process)
                    {
                        case RequirementItemProcessMode.none:
                            break;
                        case RequirementItemProcessMode.durabilityChange:
                            if (processParameter > 0)
                            {
                                int durabilityMissing = collectible.GetMaxDurability(slot.Itemstack) - slot.Itemstack.Attributes.GetInt("durability"); ;
                                match = durabilityMissing >= processParameter;
                            }
                            else if (processParameter < 0)
                            {
                                match = -processParameter >= slot.Itemstack.Attributes.GetInt("durability");
                            }
                            break;
                        case RequirementItemProcessMode.durabilityDamage:
                            break;
                        case RequirementItemProcessMode.consumeAmount:
                            if (processParameter > 0)
                            {
                                match = processParameter >= slot.Itemstack.StackSize;
                            }
                            break;
                    }
                }

                switch (mode)
                {
                    case RequirementSearchMode.whitelist:
                        break;
                    case RequirementSearchMode.blacklist:
                        match = !match;
                        break;
                }

                return match;
            }

            private void ChangeDurability(ItemStack itemstack, int amount)
            {
                if (amount >= 0 && itemstack.Collectible.GetRemainingDurability(itemstack) >= itemstack.Collectible.GetMaxDurability(itemstack))
                {
                    return;
                }

                int remainingDurability = itemstack.Collectible.GetRemainingDurability(itemstack) + amount;
                remainingDurability = Math.Min(itemstack.Collectible.GetMaxDurability(itemstack), remainingDurability);

                if (remainingDurability < 0)
                {
                    return;
                }

                itemstack.Attributes.SetInt("durability", Math.Max(remainingDurability, 0));
            }

            private List<ItemSlot> CheckInventory(EntityAgent byEntity, bool foundAll = true)
            {
                List<ItemSlot> slots = new();

                byEntity.WalkInventory((inventorySlot) =>
                {
                    if (CheckSlot(inventorySlot))
                    {
                        slots.Add(inventorySlot);
                        return foundAll;
                    }

                    return true;
                });

                return slots;
            }
        }

        public class TickBasedTimer
        {
            private readonly ICoreAPI mApi;
            private readonly Action<float> mCallback;
            
            private float mDuration_ms;
            private long? mCallbackId;
            private float mCurrentDuration = 0;
            private float mCurrentProgress = 0;
            private bool mForward = true;
            private bool mAutoStop;

            public TickBasedTimer(ICoreAPI api, int duration_ms, Action<float> callback, bool autoStop = true, float startingProgress = 0)
            {
                mApi = api;
                mDuration_ms = (float)duration_ms / 1000;
                mCallback = callback;
                mAutoStop = autoStop;
                mCurrentProgress = startingProgress;
                mCurrentDuration = mCurrentProgress * duration_ms;
                StartListener();
            }
            public void Stop()
            {
                StopListener();
            }
            public void Resume(int? duration_ms = null, bool? autoStop = null)
            {
                if (duration_ms != null) mDuration_ms = (float)duration_ms / 1000;
                mCurrentDuration = mDuration_ms * mCurrentProgress;
                mForward = true;
                mAutoStop = autoStop == null ? mAutoStop : (bool)autoStop;
                StartListener();
            }
            public void Revert(int? duration_ms = null, bool? autoStop = null)
            {
                if (duration_ms != null) mDuration_ms = (float)duration_ms / 1000;
                mCurrentDuration = mDuration_ms * (1 - mCurrentProgress);
                mForward = false;
                mAutoStop = autoStop == null ? mAutoStop : (bool)autoStop;
                StartListener();
            }

            private void Handler(float time)
            {
                mCurrentDuration += time;
                mCallback(CalculateProgress(mCurrentDuration));
                if (mAutoStop && mCurrentDuration >= mDuration_ms) StopListener();
            }
            private float CalculateProgress(float time)
            {
                float progress = GameMath.Clamp(time / mDuration_ms, 0, 1);
                mCurrentProgress = mForward ? progress : 1 - progress;
                return mCurrentProgress;
            }
            private void StartListener()
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
}
