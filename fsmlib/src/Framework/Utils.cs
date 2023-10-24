using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;

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
            first.Translation = first.Translation + second.Translation;
            first.Rotation = first.Rotation + second.Rotation;
            first.Origin = first.Origin + second.Origin;
            first.ScaleXYZ.X = first.ScaleXYZ.X * second.ScaleXYZ.X;
            first.ScaleXYZ.Y = first.ScaleXYZ.Y * second.ScaleXYZ.Y;
            first.ScaleXYZ.Z = first.ScaleXYZ.Z * second.ScaleXYZ.Z;
            return output;
        }
    }
}
