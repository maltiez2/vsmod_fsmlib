using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace MaltiezFSM.Framework
{
    public static class Utils
    {
        internal static class Logger
        {
            private static ILogger sLogger;
            private static bool sDebugLogging;
            private const string cPrefix = "[FSMlib]";

            public static void Init(ILogger logger, bool debugLogging = true)
            {
                sLogger = logger;
                sDebugLogging = debugLogging;
            }

            public static void Notify(object caller, string format, params object[] arguments) => sLogger?.Notification(Format(caller, format), arguments);
            public static void Notify(ICoreAPI api, object caller, string format, params object[] arguments) => api?.Logger?.Notification(Format(caller, format), arguments);
            public static void Warn(object caller, string format, params object[] arguments) => sLogger?.Warning(Format(caller, format), arguments);
            public static void Warn(ICoreAPI api, object caller, string format, params object[] arguments) => api?.Logger?.Warning(Format(caller, format), arguments);
            public static void Error(object caller, string format, params object[] arguments) => sLogger?.Error(Format(caller, format), arguments);
            public static void Error(ICoreAPI api, object caller, string format, params object[] arguments) => api?.Logger?.Error(Format(caller, format), arguments);
            public static void Debug(object caller, string format, params object[] arguments)
            {
                if (sDebugLogging) sLogger?.Debug(Format(caller, format), arguments);
            }
            public static void Debug(ICoreAPI api, object caller, string format, params object[] arguments)
            {
                if (sDebugLogging) api?.Logger?.Debug(Format(caller, format), arguments);
            }
            private static string Format(object caller, string format) => $"{cPrefix} [{TypeName(caller)}] {format}";
            private static string TypeName(object caller)
            {
                Type type = caller.GetType();

                if (type.IsGenericType)
                {
                    string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    string genericParameters = type.GetGenericArguments().Select(TypeName).Aggregate((first, second) => $"{first},{second}");
                    return $"{namePrefix}<{genericParameters}>";
                }

                return type.Name;
            }
        }

        static public ModelTransform ToTransformFrom(JsonObject transform, float multiplier = 1)
        {
            if (transform == null) return null;

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
            Vec3f vertical = new(0, 1, 0);
            Vec3f localZ = viewVector.Normalize();
            Vec3f localX = viewVector.Cross(vertical).Normalize();
            Vec3f localY = localX.Cross(localZ);
            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public Vec3d FromCameraReferenceFrame(EntityAgent player, Vec3d position)
        {
            Vec3f viewVectorF = player.SidedPos.GetViewVector();
            Vec3d viewVector = new(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
            Vec3d vertical = new(0, 1, 0);
            Vec3d localZ = viewVector.Normalize();
            Vec3d localX = viewVector.Cross(vertical).Normalize();
            Vec3d localY = localX.Cross(localZ);
            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public Vec3d ToCameraReferenceFrame(EntityAgent player, Vec3d position)
        {
            Vec3f viewVectorF = player.SidedPos.GetViewVector();
            Vec3d viewVector = new(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
            Vec3d vertical = new(0, 1, 0);
            Vec3d localZ = viewVector.Normalize();
            Vec3d localX = viewVector.Cross(vertical).Normalize();
            Vec3d localY = localX.Cross(localZ);

            InverseMatrix(localX, localY, localZ);

            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public Vec3d ToReferenceFrame(Vec3d reference, Vec3d position)
        {
            Vec3d vertical = new(0, 1, 0);
            Vec3d localZ = reference.Normalize();
            Vec3d localX = reference.Cross(vertical).Normalize();
            Vec3d localY = localX.Cross(localZ);

            InverseMatrix(localX, localY, localZ);

            return localX * position.X + localY * position.Y + localZ * position.Z;
        }
        static public Vec3f ToReferenceFrame(Vec3f reference, Vec3f position)
        {
            Vec3f vertical = new(0, 1, 0);
            Vec3f localZ = reference.Normalize();
            Vec3f localX = reference.Cross(vertical).Normalize();
            Vec3f localY = localX.Cross(localZ);

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
        static public void InverseMatrix(Vec3f X, Vec3f Y, Vec3f Z)
        {
            float[] matrix = { X.X, X.Y, X.Z, Y.X, Y.Y, Y.Z, Z.X, Z.Y, Z.Z };
            Mat3f.Invert(matrix, matrix);
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

        static public short[] FromModelTransform(ModelTransform transform)
        {
            return new short[]
            {
                SerializeFloatPreciseEnough(transform.Translation.X), SerializeFloatPreciseEnough(transform.Translation.Y), SerializeFloatPreciseEnough(transform.Translation.Z),
                SerializeFloatPreciseEnough(transform.Rotation.X), SerializeFloatPreciseEnough(transform.Rotation.Y), SerializeFloatPreciseEnough(transform.Rotation.Z),
                SerializeFloatPreciseEnough(transform.Origin.X), SerializeFloatPreciseEnough(transform.Origin.Y), SerializeFloatPreciseEnough(transform.Origin.Z),
                SerializeFloatPreciseEnough(transform.ScaleXYZ.X), SerializeFloatPreciseEnough(transform.ScaleXYZ.Y), SerializeFloatPreciseEnough(transform.ScaleXYZ.Z)
            };
        }
        static public ModelTransform ToModelTransform(short[] transform)
        {
            return new ModelTransform()
            {
                Translation = new Vec3f() { X = DeserializeFloatPreciseEnough(transform[0]), Y = DeserializeFloatPreciseEnough(transform[1]), Z = DeserializeFloatPreciseEnough(transform[2]) },
                Rotation = new Vec3f() { X = DeserializeFloatPreciseEnough(transform[3]), Y = DeserializeFloatPreciseEnough(transform[4]), Z = DeserializeFloatPreciseEnough(transform[5]) },
                Origin = new Vec3f() { X = DeserializeFloatPreciseEnough(transform[6]), Y = DeserializeFloatPreciseEnough(transform[7]), Z = DeserializeFloatPreciseEnough(transform[8]) },
                ScaleXYZ = new Vec3f() { X = DeserializeFloatPreciseEnough(transform[9]), Y = DeserializeFloatPreciseEnough(transform[10]), Z = DeserializeFloatPreciseEnough(transform[11]) }
            };
        }

        static public short SerializeFloatPreciseEnough(float value)
        {
            return (short)(value * 100);
        }
        static public float DeserializeFloatPreciseEnough(short value)
        {
            return (float)value / 100;
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

        public struct DirectionOffset
        {
            /// <summary>
            /// In radians. Positive direction: top.
            /// </summary>
            public float pitch { get; set; }
            /// <summary>
            /// In radians. Positive direction: right.
            /// </summary>
            public float yaw { get; set; }

            public static implicit operator DirectionOffset((float pitch, float yaw) parameters)
            {
                return new DirectionOffset() { pitch = parameters.pitch, yaw = parameters.yaw };
            }

            public DirectionOffset(Vec3d direction, Vec3d reference)
            {
                float[] from = new[] { (float)reference.X, (float)reference.Y, (float)reference.Z };
                float[] to = new[] { (float)direction.X, (float)direction.Y, (float)direction.Z };

                float yawSin = (from[2] * to[0] - from[0] * to[2]) / MathF.Sqrt((from[0] * from[0] + from[2] * from[2]) * (to[0] * to[0] + to[2] * to[2]));
                float pitchSin = (from[2] * to[1] - from[1] * to[2]) / MathF.Sqrt((from[1] * from[1] + from[2] * from[2]) * (to[1] * to[1] + to[2] * to[2]));
                yaw = MathF.Asin(yawSin);
                pitch = MathF.Asin(pitchSin);
            }

            public DirectionOffset(Vec3f direction, Vec3f reference)
            {
                float yawSin = (reference.Z * direction.X - reference.X * direction.Z) / MathF.Sqrt((reference.X * reference.X + reference.Z * reference.Z) * (direction.X * direction.X + direction.Z * direction.Z));
                float pitchSin = (reference.Z * direction.Y - reference.Y * direction.Z) / MathF.Sqrt((reference.Y * reference.Y + reference.Z * reference.Z) * (direction.Y * direction.Y + direction.Z * direction.Z));
                yaw = MathF.Asin(yawSin);
                pitch = MathF.Asin(pitchSin);
            }
        }
        public class DirectionConstrain
        {
            /// <summary>
            /// In radians. Positive direction: top.
            /// </summary>
            public float pitchTop { get; set; }
            /// <summary>
            /// In radians. Positive direction: top.
            /// </summary>
            public float pitchBottom { get; set; }
            /// <summary>
            /// In radians. Positive direction: right.
            /// </summary>
            public float yawLeft { get; set; }
            /// <summary>
            /// In radians. Positive direction: right.
            /// </summary>
            public float yawRight { get; set; }

            public static implicit operator DirectionConstrain((float pitchTop, float pitchBottom, float yawLeft, float yawRight) parameters)
            {
                return new DirectionConstrain() { pitchTop = parameters.pitchTop, pitchBottom = parameters.pitchBottom, yawLeft = parameters.yawLeft, yawRight = parameters.yawRight };
            }

            public bool Check(DirectionOffset offset)
            {
                if (
                    offset.pitch > pitchTop ||
                    offset.pitch < pitchBottom ||
                    offset.yaw < yawLeft ||
                    offset.yaw > yawRight

                ) return false;

                return true;
            }
        }

        static public class DamageModifiers
        {
            public delegate void Modifier(ref float damage, float value);
            public delegate void TieredModifier(ref float damage, ref int tier);

            public readonly static Modifier Multiply = (ref float damage, float value) => damage *= value;
            public readonly static Modifier Divide = (ref float damage, float value) => damage = value == 0 ? damage : damage / value;
            public readonly static Modifier Add = (ref float damage, float value) => damage += value;
            public readonly static Modifier Subtract = (ref float damage, float value) => damage -= value;

            public static Modifier Get(string name)
            {
                switch (name)
                {
                    case "Multiply": return Multiply;
                    case "Divide": return Divide;
                    case "Add": return Add;
                    case "Subtract": return Subtract;
                    default: throw new NotImplementedException();
                }
            }

            public static TieredModifier GetTiered(JsonObject definition)
            {
                string modifierType = definition["type"].AsString("Multiply");
                float defaultModifier = definition["default"].AsFloat(1);
                List<int> thresholds = new();
                List<float> modifiers = new();

                if (definition.KeyExists("thresholds"))
                {
                    foreach (JsonObject threshold in definition["thresholds"].AsArray())
                    {
                        thresholds.Add(threshold.AsInt());
                    }
                }

                if (definition.KeyExists("modifiers"))
                {
                    foreach (JsonObject threshold in definition["modifiers"].AsArray())
                    {
                        modifiers.Add(threshold.AsFloat());
                    }
                }

                return (ref float damage, ref int tier) => Get(modifierType)(ref damage, CalcModifier(tier, 0, thresholds, modifiers, defaultModifier));
            }

            private static float CalcModifier(float tier, int depth, List<int> thresholds, List<float> modifiers, float defaultModifier)
            {
                if (depth >= thresholds.Count) return defaultModifier;
                if (tier <= thresholds[depth]) return modifiers[depth];
                return CalcModifier(tier, depth + 1, thresholds, modifiers, defaultModifier);
            }
        }

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
            public RequirementItemProcessMode process { get; set; }
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
                                int durabilityMissing = collectible.GetMaxDurability(slot.Itemstack) - slot.Itemstack.Attributes.GetInt("durability");
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

            private float mDuration;
            private long? mCallbackId;
            private float mCurrentDuration = 0;
            private float mCurrentProgress = 0;
            private bool mForward = true;
            private bool mAutoStop;

            public TickBasedTimer(ICoreAPI api, TimeSpan duration, Action<float> callback, bool autoStop = true, float startingProgress = 0)
            {
                mApi = api;
                mDuration = (float)duration.TotalSeconds;
                mCallback = callback;
                mAutoStop = autoStop;
                mCurrentProgress = startingProgress;
                mCurrentDuration = mCurrentProgress * mDuration;
                StartListener();
            }
            public void Stop()
            {
                StopListener();
            }
            public void Resume(int? duration_ms = null, bool? autoStop = null)
            {
                if (duration_ms != null) mDuration = (float)duration_ms / 1000;
                mCurrentDuration = mDuration * mCurrentProgress;
                mForward = true;
                mAutoStop = autoStop == null ? mAutoStop : (bool)autoStop;
                StartListener();
            }
            public void Revert(int? duration_ms = null, bool? autoStop = null)
            {
                if (duration_ms != null) mDuration = (float)duration_ms / 1000;
                mCurrentDuration = mDuration * (1 - mCurrentProgress);
                mForward = false;
                mAutoStop = autoStop == null ? mAutoStop : (bool)autoStop;
                StartListener();
            }

            private void Handler(float time)
            {
                mCurrentDuration += time;
                mCallback(CalculateProgress(mCurrentDuration));
                if (mAutoStop && mCurrentDuration >= mDuration) StopListener();
            }
            private float CalculateProgress(float time)
            {
                float progress = GameMath.Clamp(time / mDuration, 0, 1);
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

        public class DelayedCallback : IDisposable
        {
            private readonly Action mCallback;
            private readonly ICoreAPI mApi;
            private long? mCallbackId;
            private bool mDisposed = false;

            public DelayedCallback(ICoreAPI api, int delayMs, Action callback)
            {
                mCallback = callback;
                mApi = api;

                mCallbackId = mApi.World.RegisterCallback(Handler, delayMs);
            }

            public void Handler(float time)
            {
                mCallback();
            }

            public void Cancel()
            {
                if (mCallbackId != null)
                {
                    mApi.World.UnregisterCallback((long)mCallbackId);
                    mCallbackId = null;
                }
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!mDisposed)
                {
                    if (disposing)
                    {
                        Cancel();
                    }

                    mDisposed = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public enum SlotType
        {
            MainHand,
            OffHand,
            Inventory,
            HotBar,
            Character,
            Backpack,
            Crafting
        }
        public struct SlotData
        {
            public SlotType SlotType { get; set; }
            public int SlotId { get; set; } = -1;
            public string InventoryId { get; set; } = "";

            public SlotData(SlotType type, ItemSlot slot = null, IPlayer player = null)
            {
                SlotType = type;

                if (slot == null || player == null) return;

                switch (type)
                {
                    case SlotType.HotBar:
                        SlotId = player.InventoryManager.GetHotbarInventory().GetSlotId(slot);
                        break;
                    case SlotType.Inventory:
                        (string inventoryId, int slotId) = GetSlotIdFromInventory(slot, player);
                        SlotId = slotId;
                        InventoryId = inventoryId;
                        break;
                    default: break;
                }
            }

            public ItemSlot Slot(IPlayer player)
            {
                switch (SlotType)
                {
                    case SlotType.HotBar:
                        return player.InventoryManager.GetHotbarInventory()[SlotId];
                    case SlotType.MainHand:
                        return player.Entity.RightHandItemSlot;
                    case SlotType.OffHand:
                        return player.Entity.LeftHandItemSlot;
                    case SlotType.Inventory:
                        return player.InventoryManager.GetInventory(InventoryId)[SlotId];
                    case SlotType.Character:
                        return player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName)[SlotId];
                    case SlotType.Backpack:
                        return player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName)[SlotId];
                    case SlotType.Crafting:
                        return player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName)[SlotId];
                    default:
                        return null;
                }
            }

            public static IEnumerable<SlotData> GetForAllSlots(SlotType type, IPlayer player = null)
            {
                HashSet<SlotData> slots = new();

                switch (type)
                {
                    case SlotType.HotBar:
                        foreach (ItemSlot hotbarSlot in player.InventoryManager.GetHotbarInventory())
                        {
                            slots.Add(new(type, hotbarSlot));
                        }
                        break;
                    case SlotType.Inventory:
                        foreach ((_, IInventory inventory) in player.InventoryManager.Inventories)
                        {
                            foreach (ItemSlot inventorySlot in inventory)
                            {
                                slots.Add(new(type, inventorySlot, player));
                            }
                        }
                        break;
                    case SlotType.Character:
                        foreach (ItemSlot characterSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName))
                        {
                            slots.Add(new(type, characterSlot));
                        }
                        break;
                    case SlotType.Backpack:
                        foreach (ItemSlot backpackSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName))
                        {
                            slots.Add(new(type, backpackSlot));
                        }
                        break;
                    case SlotType.Crafting:
                        foreach (ItemSlot craftingSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName))
                        {
                            slots.Add(new(type, craftingSlot));
                        }
                        break;
                    default:
                        slots.Add(new(type));
                        break;
                }

                return slots;
            }

            public static IEnumerable<SlotData> GetForAllSlots(SlotType type, CollectibleObject collectible, IPlayer player = null)
            {
                HashSet<SlotData> slots = new();

                switch (type)
                {
                    case SlotType.HotBar:
                        foreach (ItemSlot hotbarSlot in player.InventoryManager.GetHotbarInventory().Where((slot) => slot?.Itemstack?.Collectible == collectible))
                        {
                            slots.Add(new(type, hotbarSlot));
                        }
                        break;
                    case SlotType.Inventory:
                        foreach ((_, IInventory inventory) in player.InventoryManager.Inventories)
                        {
                            foreach (ItemSlot inventorySlot in inventory.Where((slot) => slot?.Itemstack?.Collectible == collectible))
                            {
                                slots.Add(new(type, inventorySlot, player));
                            }
                        }
                        break;
                    case SlotType.MainHand:
                        if (player?.Entity?.RightHandItemSlot?.Itemstack?.Collectible == collectible) slots.Add(new(type));
                        break;
                    case SlotType.OffHand:
                        if (player?.Entity?.LeftHandItemSlot?.Itemstack?.Collectible == collectible) slots.Add(new(type));
                        break;
                    case SlotType.Character:
                        foreach (ItemSlot characterSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName).Where((slot) => slot?.Itemstack?.Collectible == collectible))
                        {
                            slots.Add(new(type, characterSlot));
                        }
                        break;
                    case SlotType.Backpack:
                        foreach (ItemSlot backpackSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName).Where((slot) => slot?.Itemstack?.Collectible == collectible))
                        {
                            slots.Add(new(type, backpackSlot));
                        }
                        break;
                    case SlotType.Crafting:
                        foreach (ItemSlot craftingSlot in player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName).Where((slot) => slot?.Itemstack?.Collectible == collectible))
                        {
                            slots.Add(new(type, craftingSlot));
                        }
                        break;
                    default:
                        slots.Add(new(type));
                        break;
                }

                return slots;
            }

            private static (string inventory, int slot) GetSlotIdFromInventory(ItemSlot slot, IPlayer player)
            {
                foreach ((_, IInventory inventory) in player.InventoryManager.Inventories)
                {
                    int slotId = inventory.GetSlotId(slot);
                    if (slotId != -1)
                    {
                        return (inventory.InventoryID, slotId);
                    }
                }

                return ("", -1);
            }
        }
    }
}
