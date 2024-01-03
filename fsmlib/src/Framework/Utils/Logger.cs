using System;
using System.Linq;
using Vintagestory.API.Common;



namespace MaltiezFSM.Framework;

internal static class Logger
{
    private static ILogger? sLogger;
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
    public static void Verbose(object caller, string format, params object[] arguments) => sLogger?.VerboseDebug(Format(caller, format), arguments);
    public static void Verbose(ICoreAPI api, object caller, string format, params object[] arguments) => api?.Logger?.VerboseDebug(Format(caller, format), arguments);
    public static string Format(object caller, string format) => $"{cPrefix} [{GetCallerTypeName(caller)}] {format}";
    public static string GetCallerTypeName(object caller)
    {
        Type type = caller.GetType();

        if (type.IsGenericType)
        {
            string namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string genericParameters = type.GetGenericArguments().Select(GetCallerTypeName).Aggregate((first, second) => $"{first},{second}");
            return $"{namePrefix}<{genericParameters}>";
        }

        return type.Name;
    }
}