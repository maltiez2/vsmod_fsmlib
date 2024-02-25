using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace MaltiezFSM.Framework;

/// <summary>
/// Allows to register and instantiate objects in runtime.<br/>
/// Instantiates objects using constructor with following arguments:
/// <list type="definition">
/// <item>
/// <term><see cref="int"/> id</term>
/// <description>unique id of this item, should be used for comparison</description>
/// </item>
/// <item>
/// <term><see cref="string"/> code</term>
/// <description>specified in definition of FSM, used to reference objects from JSON</description>
/// </item>
/// <item>
/// <term><see cref="JsonObject"/> definition</term>
/// <description>stores all parameters given to this specific object in json</description>
/// </item>
/// <item>
/// <term><see cref="CollectibleObject"/> collectible</term>
/// <description>collectible associated with this object</description>
/// </item>
/// <item>
/// <term><see cref="ICoreAPI"/> api</term>
/// <description>just api</description>
/// </item>
/// </list>
/// </summary>
/// <typeparam name="TProduct">Base type of instantiated objects</typeparam>
internal sealed class Factory<TProduct> : IFactory<TProduct>
    where TProduct : IFactoryProduct
{
    private readonly Dictionary<string, Type> mProducts = new();
    private readonly IUniqueIdGeneratorForFactory mIdGenerator;
    private readonly ICoreAPI mApi;

    /// <summary>
    /// Just stores api and generator for future use
    /// </summary>
    /// <param name="api"></param>
    /// <param name="idGenerator">Used to give items unique id. Each <see cref="IUniqueIdGeneratorForFactory.GenerateInstanceId"/> call should generate unique id across all factories for specific FSM</param>
    public Factory(ICoreAPI api, IUniqueIdGeneratorForFactory idGenerator)
    {
        mApi = api;
        mIdGenerator = idGenerator;
    }
    /// <summary>
    /// Type of registered item
    /// </summary>
    /// <param name="name">name given to <see cref="Register"/> method</param>
    /// <returns></returns>
    public Type TypeOf(string name)
    {
        return mProducts[name];
    }
    /// <summary>
    /// Tries to add <see cref="TObjectClass"/> to list of registered types. Returns false if some type with given <paramref name="name"/> already registered.
    /// </summary>
    /// <typeparam name="TObjectClass">Type to register</typeparam>
    /// <param name="name">name that will be specified in "class" field in JSON</param>
    /// <returns>false if some type with given <paramref name="name"/> already registered</returns>
    public bool Register<TObjectClass>(string name) where TObjectClass : FactoryProduct, TProduct
    {
        return mProducts.TryAdd(name, typeof(TObjectClass));
    }
    /// <summary>
    /// Assigns <typeparamref name="TObjectClass"/> type to given <paramref name="name"/> without check weather it was already existed.
    /// </summary>
    /// <typeparam name="TObjectClass">Type to register</typeparam>
    /// <param name="name">name that will be specified in "class" field in JSON</param>
    public void SubstituteWith<TObjectClass>(string name) where TObjectClass : FactoryProduct, TProduct
    {
        mProducts[name] = typeof(TObjectClass);
    }
    /// <summary>
    /// Creates instance of a type registered under given <paramref name="name"/>.
    /// </summary>
    /// <param name="code">specified in JSON in definition, used to reference this instance of the object from JSON</param>
    /// <param name="name">name of registered type</param>
    /// <param name="definition">parameters from JSON that will be passed to object constructor</param>
    /// <param name="collectible">collectible this object attached to</param>
    /// <returns>instantiated object or null if given <paramref name="name"/> was not registered or instantiation failed</returns>
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
