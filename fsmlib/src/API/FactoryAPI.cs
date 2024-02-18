using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.API;

public interface IFactoryProduct
{
    int Id { get; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3246:Generic type parameters should be co/contravariant when possible", Justification = "Static analyzer is dumb sometimes")]
public interface IFactory<TProductInterface>
{
    Type TypeOf(string name);
    bool Register<TProductClass>(string name) where TProductClass : FactoryProduct, TProductInterface;
    void SubstituteWith<TProductClass>(string name) where TProductClass : FactoryProduct, TProductInterface;
    TProductInterface? Instantiate(string code, string name, JsonObject definition, CollectibleObject collectible);
}

internal interface IFactoryProvider
{
    IFactory<IOperation>? GetOperationFactory();
    IFactory<ISystem>? GetSystemFactory();
    IFactory<IInput>? GetInputFactory();
}
public interface IRegistry
{
    [Obsolete("Use RegisterOperation<TProductClass>(string name, ICoreAPI api, Mod mod) instead")]
    void RegisterOperation<TProductClass>(string name) where TProductClass : FactoryProduct, IOperation;
    [Obsolete("Use RegisterSystem<TProductClass>(string name, ICoreAPI api, Mod mod) instead")]
    void RegisterSystem<TProductClass>(string name) where TProductClass : FactoryProduct, ISystem;
    [Obsolete("Use RegisterInput<TProductClass>(string name, ICoreAPI api, Mod mod) instead")]
    void RegisterInput<TProductClass>(string name) where TProductClass : FactoryProduct, IStandardInput;
    [Obsolete("Use RegisterInput<TProductClass>(string name, ICoreAPI api, Mod mod) instead")]
    void RegisterInput<TProductClass, TInputInterface>(string name, IInputInvoker invoker)
        where TInputInterface : IInput
        where TProductClass : FactoryProduct, IInput;

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
