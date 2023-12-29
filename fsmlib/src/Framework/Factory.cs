using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

#nullable enable

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
                Utils.Logger.Warn(mApi, this, $"Type '{name}' (code: '{code}') is not registered, will skip it.");
                return default;
            }
            
            TProduct? product = default;
            
            try
            {
                product = (TProduct?)Activator.CreateInstance(mProducts[name], mIdGenerator.GenerateInstanceId(), code, definition, collectible, mApi);
            }
            catch (Exception exception)
            {
                Utils.Logger.Error(mApi, this, $"Exception on instantiating {name} ({Utils.TypeName(mProducts[name])}) with code '{code}' for collectible '{collectible?.Code}'");
                Utils.Logger.Verbose(mApi, this, $"Exception on instantiating {name} ({Utils.TypeName(mProducts[name])}) with code '{code}' for collectible '{collectible?.Code}'.\n\nDefinition:{definition}\n\nException:{exception}");
            }

            return product;
        }
    }
}
