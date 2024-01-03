using MaltiezFSM.Framework;
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
    }
}
