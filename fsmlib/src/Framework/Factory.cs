using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

#nullable enable

namespace MaltiezFSM.Framework
{
    internal class Factory<TProductClass> : IFactory<TProductClass>
        where TProductClass : IFactoryProduct
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
        public void Register<TObjectClass>(string name) where TObjectClass : FactoryProduct, TProductClass
        {
            mProducts.Add(name, typeof(TObjectClass));
        }
        public void SubstituteWith<TObjectClass>(string name) where TObjectClass : FactoryProduct, TProductClass
        {
            mProducts[name] = typeof(TObjectClass);
        }
        public TProductClass? Instantiate(string code, string name, JsonObject definition, CollectibleObject collectible)
        {
            if (!mProducts.ContainsKey(name))
            {
                Utils.Logger.Warn(mApi, this, $"Type '{name}' (code: '{code}') is not registered, will skip it.");
                return default;
            }
            TProductClass? product = (TProductClass?)Activator.CreateInstance(mProducts[name], mIdGenerator.GenerateInstanceId(), code, definition, collectible, mApi);
            return product;
        }
    }
}
