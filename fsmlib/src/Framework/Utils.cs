using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace MaltiezFSM.Framework
{
    public static class Utils
    {
        static public ModelTransform ToTransformFrom(JsonObject transform, float multiplier = 1)
        {
            JsonObject translation = transform["translation"];
            JsonObject rotation = transform["rotation"];
            JsonObject origin = transform["origin"];

            ModelTransform modelTransform = new ModelTransform();
            modelTransform.EnsureDefaultValues();
            modelTransform.Translation.Set(multiplier * translation["x"].AsFloat(), multiplier * translation["y"].AsFloat(), multiplier * translation["z"].AsFloat());
            modelTransform.Rotation.Set(multiplier * rotation["x"].AsFloat(), multiplier * rotation["y"].AsFloat(), multiplier * rotation["z"].AsFloat());
            modelTransform.Origin.Set(multiplier * origin["x"].AsFloat(), multiplier * origin["y"].AsFloat(), multiplier * origin["z"].AsFloat());
            modelTransform.Scale = transform["scale"].AsFloat(1);
            return modelTransform;
        }

        static public ModelTransform CombineTransforms(ModelTransform first, ModelTransform second)
        {
            ModelTransform output = first.Clone();
            output.Translation = first.Translation + second.Translation;
            output.Rotation = first.Rotation + second.Rotation;
            output.Origin = first.Origin + second.Origin;
            output.ScaleXYZ.X = first.ScaleXYZ.X * second.ScaleXYZ.X;
            output.ScaleXYZ.Y = first.ScaleXYZ.Y * second.ScaleXYZ.Y;
            output.ScaleXYZ.Z = first.ScaleXYZ.Z * second.ScaleXYZ.Z;
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

        static public Vec3f TransitionVector(Vec3f from, Vec3f to, float progress)
        {
            return from + (to - from) * progress;
        }
    }
}
