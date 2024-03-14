using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace MaltiezFSM.API;

/// <summary>
/// Convenience class that implements methods and fields that are commonly used by all types of objects (systems, operations, inputs)
/// </summary>
public class FactoryProduct : IFactoryProduct
{
    /// <summary>
    /// Unique id supplied on instantiating by factory
    /// </summary>
    public int Id => mId;

    /// <summary>
    /// Unique id supplied on instantiating by factory
    /// </summary>
    private readonly int mId;
    /// <summary>
    /// Code to reference this object from JSON
    /// </summary>
    protected readonly string mCode;
    /// <summary>
    /// Collectible associated with this object
    /// </summary>
    protected readonly CollectibleObject mCollectible;
    /// <summary>
    /// <see cref="ICoreClientAPI"/> or <see cref="ICoreServerAPI"/> depending on what side it was instantiated
    /// </summary>
    protected readonly ICoreAPI mApi;

    /// <summary>
    /// Just stores supplied parameters
    /// </summary>
    /// <param name="id">unique id given by factory on instantiating</param>
    /// <param name="code">code to reference this object in JSON</param>
    /// <param name="definition">parameters from JSON</param>
    /// <param name="collectible">collectible this object is associated with</param>
    /// <param name="api">just api</param>
    public FactoryProduct(int id, string code, JsonObject? definition, CollectibleObject collectible, ICoreAPI api)
    {
        mId = id;
        mCode = code;
        mCollectible = collectible;
        mApi = api;
    }

    /// <summary>
    /// Factory products have unique id, so only it needs to be compared.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj) => (obj as FactoryProduct)?.Id == mId;
    /// <summary>
    /// Factory products have unique id, so it can be easily used as hash.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => mId;

    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogError(string message) => Logger.Error(mApi, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogWarn(string message) => Logger.Warn(mApi, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogNotify(string message) => Logger.Notify(mApi, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogDebug(string message) => Logger.Debug(mApi, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogVerbose(string message) => Logger.Verbose(mApi, this, LogFormat(message));
    /// <summary>
    /// Wrapper around logger method with api and this already passed to its arguments, and also prepends collectible code and mCode.<br/>
    /// Logs only in DEBUG build.
    /// </summary>
    /// <param name="message">message to log</param>
    protected void LogDev(string message) => Logger.Dev(mApi, this, LogFormat(message));

    /// <summary>
    /// Wraps supplied message
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    protected string LogFormat(string message) => $"({mCollectible.Code}:{mCode}) {message}";

    /// <summary>
    /// Convenience method that collects values from JsonObject field if this field exists, and if this field is array, it collects all elements from array.<br/>
    /// Try to use it when possible to reduce amount of text in JSON needed.
    /// </summary>
    /// <param name="definition">where to look field in</param>
    /// <param name="field">field to collect values from</param>
    /// <returns>empty if field was not found or it was an empty array</returns>
    protected static List<JsonObject> ParseField(JsonObject definition, string field)
    {
        List<JsonObject> fields = new();
        if (!definition.KeyExists(field)) return fields;

        if (definition[field].IsArray())
        {
            foreach (JsonObject fieldObject in definition[field].AsArray())
            {
                fields.Add(fieldObject);
            }
        }
        else
        {
            fields.Add(definition[field]);
        }
        return fields;
    }
    /// <summary>
    /// Collects values from supplied object, if object is an array, it collects all element values from it.
    /// </summary>
    /// <param name="definition">object to collect values from, if object is not an array it will be just put into result list</param>
    /// <returns>empty if was an empty array</returns>
    protected static List<JsonObject> ParseField(JsonObject definition)
    {
        List<JsonObject> fields = new();

        if (definition.IsArray())
        {
            foreach (JsonObject fieldObject in definition.AsArray())
            {
                fields.Add(fieldObject);
            }
        }
        else
        {
            fields.Add(definition);
        }
        return fields;
    }
}
