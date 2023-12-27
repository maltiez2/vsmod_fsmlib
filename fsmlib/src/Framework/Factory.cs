using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

namespace MaltiezFSM.Framework
{
    internal class Factory<TProductClass, TIdGeneratorClass> : IFactory<TProductClass>
        where TProductClass : IFactoryProduct
        where TIdGeneratorClass : IUniqueIdGeneratorForFactory, new()  
    {
        private readonly Dictionary<string, Type> mProducts = new();
        private readonly IUniqueIdGeneratorForFactory mIdGenerator = new TIdGeneratorClass();
        private readonly ICoreAPI mApi;

        public Factory(ICoreAPI api)
        {
            mApi = api;
        }
        public Type TypeOf(string name)
        {
            return mProducts[name];
        }
        public void Register<TObjectClass>(string name) where TObjectClass : TProductClass, new()
        {
            mProducts.Add(name, typeof(TObjectClass));
        }
        public void SubstituteWith<TObjectClass>(string name) where TObjectClass : TProductClass, new()
        {
            mProducts[name] = typeof(TObjectClass);
        }
        public TProductClass Instantiate(string code, string name, JsonObject definition, CollectibleObject collectible)
        {
            if (!mProducts.ContainsKey(name))
            {
                Utils.Logger.Warn(mApi, this, $"Type '{name}' (code: '{code}') is not registered, will skip it.");
                return default;
            }
            TProductClass producedInstance = (TProductClass)Activator.CreateInstance(mProducts[name]);
            producedInstance.Init(code, definition, collectible, mApi);
            producedInstance.SetId(mIdGenerator.GenerateInstanceId());
            return producedInstance;
        }
    }
}
