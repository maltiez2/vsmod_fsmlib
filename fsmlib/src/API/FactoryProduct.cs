using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable enable

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
    }
}
