﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace MaltiezFSM.Framework;

public static class Utils
{
    public static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string genericParameters = type.GetGenericArguments().Select(GetTypeName).Aggregate((first, second) => $"{first},{second}");
            return $"{namePrefix}<{genericParameters}>";
        }

        return type.Name;
    }

    static public ModelTransform? ToTransformFrom(JsonObject transform, float multiplier = 1)
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
        ModelTransform output = new()
        {
            Translation = new(0, 0, 0),
            Rotation = new(0, 0, 0),
            Origin = new(0, 0, 0),
            ScaleXYZ = new(1, 1, 1)
        };
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
        Vec3f viewVector = player.SidedPos.GetViewVector().Normalize();
        Vec3f vertical = new(0, 1, 0);
        Vec3f localZ = viewVector;
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
    static public Vec3f ToCameraReferenceFrame(EntityAgent player, Vec3f position)
    {
        Vec3f viewVectorF = player.SidedPos.GetViewVector();
        Vec3f viewVector = new(viewVectorF.X, viewVectorF.Y, viewVectorF.Z);
        Vec3f vertical = new(0, 1, 0);
        Vec3f localZ = viewVector.Normalize();
        Vec3f localX = viewVector.Cross(vertical).Normalize();
        Vec3f localY = localX.Cross(localZ);

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
    public static TArray? GetArray<TArray>(this ITreeAttribute tree, string key, TArray? defaultValue = default)
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
        public float Pitch { get; set; }
        /// <summary>
        /// In radians. Positive direction: right.
        /// </summary>
        public float Yaw { get; set; }

        public static implicit operator DirectionOffset((float pitch, float yaw) parameters)
        {
            return new DirectionOffset() { Pitch = parameters.pitch, Yaw = parameters.yaw };
        }

        public DirectionOffset(Vec3d direction, Vec3d reference)
        {
            float[] from = new[] { (float)reference.X, (float)reference.Y, (float)reference.Z };
            float[] to = new[] { (float)direction.X, (float)direction.Y, (float)direction.Z };

            float yawSin = (from[2] * to[0] - from[0] * to[2]) / MathF.Sqrt((from[0] * from[0] + from[2] * from[2]) * (to[0] * to[0] + to[2] * to[2]));
            float pitchSin = (from[2] * to[1] - from[1] * to[2]) / MathF.Sqrt((from[1] * from[1] + from[2] * from[2]) * (to[1] * to[1] + to[2] * to[2]));
            Yaw = MathF.Asin(yawSin);
            Pitch = MathF.Asin(pitchSin);
        }

        public DirectionOffset(Vec3f direction, Vec3f reference)
        {
            float yawSin = (reference.Z * direction.X - reference.X * direction.Z) / MathF.Sqrt((reference.X * reference.X + reference.Z * reference.Z) * (direction.X * direction.X + direction.Z * direction.Z));
            float pitchSin = (reference.Z * direction.Y - reference.Y * direction.Z) / MathF.Sqrt((reference.Y * reference.Y + reference.Z * reference.Z) * (direction.Y * direction.Y + direction.Z * direction.Z));
            Yaw = MathF.Asin(yawSin);
            Pitch = MathF.Asin(pitchSin);
        }

        public DirectionOffset(float pitch, float yaw)
        {
            Yaw = yaw;
            Pitch = pitch;
        }

        public override string ToString() => $"Pitch: {Pitch}, Yaw: {Yaw}";
    }
    public class DirectionConstrain
    {
        /// <summary>
        /// In radians. Positive direction: top.
        /// </summary>
        public float PitchTop { get; set; }
        /// <summary>
        /// In radians. Positive direction: top.
        /// </summary>
        public float PitchBottom { get; set; }
        /// <summary>
        /// In radians. Positive direction: right.
        /// </summary>
        public float YawLeft { get; set; }
        /// <summary>
        /// In radians. Positive direction: right.
        /// </summary>
        public float YawRight { get; set; }

        public static implicit operator DirectionConstrain((float pitchTop, float pitchBottom, float yawLeft, float yawRight) parameters)
        {
            return new DirectionConstrain() { PitchTop = parameters.pitchTop, PitchBottom = parameters.pitchBottom, YawLeft = parameters.yawLeft, YawRight = parameters.yawRight };
        }

        public bool Check(DirectionOffset offset)
        {
            return offset.Pitch <= PitchTop &&
                offset.Pitch >= PitchBottom &&
                offset.Yaw >= YawLeft &&
                offset.Yaw <= YawRight;
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
            return name switch
            {
                "Multiply" => Multiply,
                "Divide" => Divide,
                "Add" => Add,
                "Subtract" => Subtract,
                _ => throw new NotImplementedException(),
            };
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

    public class TickBasedTimer
    {
        private readonly ICoreAPI mApi;
        private readonly Action<float> mCallback;
        private readonly float mDuration;
        private readonly long mCallbackId;
        
        private float mCurrentDuration = 0;

        public TickBasedTimer(ICoreAPI api, TimeSpan duration, Action<float> callback, float startingProgress = 0)
        {
            mApi = api;
            mDuration = (float)duration.TotalSeconds;
            mCallback = callback;
            mCurrentDuration = startingProgress * mDuration;
            mCallbackId = mApi.World.RegisterGameTickListener(Handler, 0);
        }
        public void Stop()
        {
            mApi.World.UnregisterGameTickListener(mCallbackId);
        }

        private void Handler(float time)
        {
            mCurrentDuration += time;
            mCallback(GameMath.Clamp(mCurrentDuration / mDuration, 0, 1));
            if (mCurrentDuration >= mDuration)
            {
                mApi.World.UnregisterGameTickListener(mCallbackId);
            }
        }
    }

    public class DelayedCallback : IDisposable
    {
        private readonly Action mCallback;
        private readonly ICoreAPI mApi;
        private long? mCallbackId;
        private bool mDisposed = false;

        public DelayedCallback(ICoreAPI api, TimeSpan delay, Action callback)
        {
            mCallback = callback;
            mApi = api;

            mCallbackId = mApi.World.RegisterCallback(Handler, (int)delay.TotalMilliseconds);
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

    public static string PrintList(IEnumerable<string> list)
    {
        StringBuilder result = new();
        bool first = true;
        foreach (string item in list)
        {
            if (first)
            {
                result.Append(item);
                first = false;
            }
            else
            {
                result.Append(", ");
                result.Append(item);
            }
        }
        return result.ToString();
    }

    public sealed class Field<TValue, TInstance>
    {
        public TValue? Value
        {
            get
            {
                return (TValue?)mFieldInfo?.GetValue(mInstance);
            }
            set
            {
                mFieldInfo?.SetValue(mInstance, value);
            }
        }

        private readonly FieldInfo? mFieldInfo;
        private readonly TInstance mInstance;

        public Field(Type from, string field, TInstance instance)
        {
            mInstance = instance;
            mFieldInfo = from.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    public static bool Iterate(JsonObject fields, Action<string, JsonObject> action)
    {
        if (fields.Token is not JObject fieldsObject) return false;

        bool fieldProcessed = false;

        foreach ((string code, JToken? fieldToken) in fieldsObject)
        {
            if (fieldToken == null || code == null) continue;

            JsonObject field = new(fieldToken);

            action.Invoke(code, field);

            fieldProcessed = true;
        }

        return fieldProcessed;
    }
}
