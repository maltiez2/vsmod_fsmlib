using Vintagestory.API.Common;

namespace MaltiezFSM.Framework.Simplified.Systems;

public abstract class BaseSystem
{
    protected BaseSystem(ICoreAPI api, string debugName = "")
    {
        Api = api;
        DebugName = debugName;
    }

    protected readonly ICoreAPI Api;
    protected readonly string DebugName;

    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogError(string message) => Logger.Error(Api, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogWarn(string message) => Logger.Warn(Api, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogNotify(string message) => Logger.Notify(Api, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogDebug(string message) => Logger.Debug(Api, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogVerbose(string message) => Logger.Verbose(Api, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.<br/>
    /// Logs only in DEBUG build.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogDev(string message) => Logger.Dev(Api, this, LogFormat(message));

    /// <summary>
    /// Wraps supplied message
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    protected string LogFormat(string message) => DebugName != "" ? $"({DebugName}) {message}" : message;
}
