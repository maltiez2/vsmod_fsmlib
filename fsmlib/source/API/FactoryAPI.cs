using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace MaltiezFSM.API;

public interface IFactoryProduct
{
    int Id { get; }
}

public interface IFactory<TProductInterface>
{
    Type TypeOf(string name);
    bool Register<TProductClass>(string name) where TProductClass : FactoryProduct, TProductInterface;
    void SubstituteWith<TProductClass>(string name) where TProductClass : FactoryProduct, TProductInterface;
    TProductInterface? Instantiate(string code, string name, JsonObject definition, CollectibleObject collectible);
}

public interface IRegistry
{
    void RegisterOperation<TProductClass>(string name, ICoreAPI api, Mod mod) where TProductClass : FactoryProduct, IOperation;
    void RegisterSystem<TProductClass>(string name, ICoreAPI api, Mod mod) where TProductClass : FactoryProduct, ISystem;
    void RegisterInput<TProductClass>(string name, ICoreAPI api, Mod mod) where TProductClass : FactoryProduct, IStandardInput;
    void RegisterInput<TProductClass, TInputInterface>(string name, IInputInvoker invoker, ICoreAPI api, Mod mod)
        where TInputInterface : IInput
        where TProductClass : FactoryProduct, IInput;
}
public interface IUniqueIdGeneratorForFactory
{
    int GenerateInstanceId();
    short GetFactoryId();
}
