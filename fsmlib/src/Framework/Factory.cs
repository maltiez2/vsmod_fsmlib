using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Framework
{
    internal sealed class Factory<TProduct> : IFactory<TProduct>
        where TProduct : IFactoryProduct
    {
        private readonly Dictionary<string, Type> mProducts = new();
        private readonly IUniqueIdGeneratorForFactory mIdGenerator;
        private readonly ICoreAPI mApi;

        public Factory(ICoreAPI api, IUniqueIdGeneratorForFactory idGenerator)
        {
            mApi = api;
            mIdGenerator = idGenerator;
        }
        public Type TypeOf(string name)
        {
            return mProducts[name];
        }
        public void Register<TObjectClass>(string name) where TObjectClass : FactoryProduct, TProduct
        {
            mProducts.TryAdd(name, typeof(TObjectClass));
        }
        public void SubstituteWith<TObjectClass>(string name) where TObjectClass : FactoryProduct, TProduct
        {
            mProducts[name] = typeof(TObjectClass);
        }
        public TProduct? Instantiate(string code, string name, JsonObject definition, CollectibleObject collectible)
        {
            if (!mProducts.ContainsKey(name))
            {
                Logger.Warn(mApi, this, $"Type '{name}' (code: '{code}') is not registered, will skip it.");
                return default;
            }

            TProduct? product = default;

            try
            {
                product = (TProduct?)Activator.CreateInstance(mProducts[name], mIdGenerator.GenerateInstanceId(), code, definition, collectible, mApi);
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"Exception on instantiating '{name} ({Utils.GetTypeName(mProducts[name])})' with code '{code}' for collectible '{collectible?.Code}'");
                Logger.Verbose(mApi, this, $"Exception on instantiating '{name} ({Utils.GetTypeName(mProducts[name])})' with code '{code}' for collectible '{collectible?.Code}'.\n\nDefinition:\n{definition}\n\nException:\n{exception}\n");
            }

            return product;
        }
    }
}
