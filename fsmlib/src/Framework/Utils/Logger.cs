using System;
using System.Linq;
using Vintagestory.API.Common;



namespace MaltiezFSM.Framework;

internal static class Logger
{
    private const string cPrefix = "[FSMlib]";

    public static void Notify(ICoreAPI? api, object caller, string format) => api?.Logger?.Notification(Format(caller, format));
    public static void Warn(ICoreAPI? api, object caller, string format) => api?.Logger?.Warning(Format(caller, format));
    public static void Error(ICoreAPI? api, object caller, string format) => api?.Logger?.Error(Format(caller, format));
    public static void Debug(ICoreAPI? api, object caller, string format) => api?.Logger?.Debug(Format(caller, format));
    public static void Verbose(ICoreAPI? api, object caller, string format) => api?.Logger?.VerboseDebug(Format(caller, format));
    public static void Dev(ICoreAPI? api, object caller, string format)
    {
#if DEBUG
        api?.Logger?.Notification($"[Dev] {Format(caller, format)}");
#endif
    }

    public static string Format(object caller, string format) => $"{cPrefix} [{GetCallerTypeName(caller)}] {format}".Replace("{","{{").Replace("}","}}");
    public static string GetCallerTypeName(object caller)
    {
        Type type = caller.GetType();

        if (type.IsGenericType)
        {
            string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string genericParameters = type.GetGenericArguments().Select(GetTypeName).Aggregate((first, second) => $"{first},{second}");
            return $"{namePrefix}<{genericParameters}>";
        }

        return type.Name;
    }
    private static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string genericParameters = type.GetGenericArguments().Select(GetTypeName).Aggregate((first, second) => $"{first},{second}");
            return $"{namePrefix}<{genericParameters}>";
        }

        return type.Name;
    }
}