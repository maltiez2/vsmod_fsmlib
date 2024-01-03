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
    void Register<TProductClass>(string name) where TProductClass : FactoryProduct, TProductInterface;
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
    void RegisterOperation<TProductClass>(string name) where TProductClass : FactoryProduct, IOperation;
    void RegisterSystem<TProductClass>(string name) where TProductClass : FactoryProduct, ISystem;
    void RegisterInput<TProductClass>(string name) where TProductClass : FactoryProduct, IStandardInput;
    void RegisterInput<TProductClass, TInputInterface>(string name, IInputInvoker invoker)
        where TInputInterface : IInput
        where TProductClass : FactoryProduct, IInput;
}
public interface IUniqueIdGeneratorForFactory
{
    int GenerateInstanceId();
    short GetFactoryId();
}
