using MaltiezFSM.Framework;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.API
{
    public class FactoryProduct : IFactoryProduct
    {
        public int Id => mId;

        private readonly int mId;
        protected readonly string mCode;
        protected readonly CollectibleObject mCollectible;
        protected readonly ICoreAPI mApi;

        public FactoryProduct(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mId = id;
            mCode = code;
            mCollectible = collectible;
            mApi = api;
        }

        public override bool Equals(object? obj) => (obj as FactoryProduct)?.Id == mId;
        public override int GetHashCode() => mId;

        protected void LogError(string message) => Logger.Error(mApi, this, LogFormat(message));
        protected void LogWarn(string message) => Logger.Warn(mApi, this, LogFormat(message));
        protected void LogNotify(string message) => Logger.Notify(mApi, this, LogFormat(message));
        protected void LogDebug(string message) => Logger.Debug(mApi, this, LogFormat(message));
        protected void LogVerbose(string message) => Logger.Verbose(mApi, this, LogFormat(message));

        protected string LogFormat(string message) => $"({mCollectible.Code}:{mCode}) {message}";

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
    }
}
