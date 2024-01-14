using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

public class BehaviourAttributesParser : IBehaviourAttributesParser
{
    private readonly Dictionary<string, IOperation> mOperations = new();
    private readonly Dictionary<string, ISystem> mSystems = new();
    private readonly Dictionary<string, IInput> mInputs = new();

    public bool ParseDefinition(ICoreAPI api, IFactory<IOperation>? operationTypes, IFactory<ISystem>? systemTypes, IFactory<IInput>? inputTypes, JsonObject behaviourAttributes, CollectibleObject collectible)
    {
        if (systemTypes == null || operationTypes == null || inputTypes == null) return false;
        
        try
        {
            Utils.Iterate(behaviourAttributes["systems"], (code, definition) => AddObject(api, "System", code, definition, collectible, systemTypes, mSystems));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating a system for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating a system for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        try
        {
            Utils.Iterate(behaviourAttributes["operations"], (code, definition) => AddObject(api, "Operation", code, definition, collectible, operationTypes, mOperations));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating an operation for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating an operation for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        try
        {
            Utils.Iterate(behaviourAttributes["inputs"], (code, definition) => AddObject(api, "Input", code, definition, collectible, inputTypes, mInputs));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating an input for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating an input for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        return true;
    }
    public Dictionary<string, IOperation> GetOperations() => mOperations;
    public Dictionary<string, ISystem> GetSystems() => mSystems;
    public Dictionary<string, IInput> GetInputs() => mInputs;

    private void AddObject<TObjectInterface>(ICoreAPI api, string objectType, string objectCode, JsonObject definition, CollectibleObject collectible, IFactory<TObjectInterface> factory, Dictionary<string, TObjectInterface> container)
    {
        string? objectClass = definition["class"].AsString();
        if (objectClass == null )
        {
            Logger.Error(api, this, $"{objectType} '{objectCode}' in '{collectible.Code}' has no class specified.");
            return;
        }
        TObjectInterface? objectInstance = factory.Instantiate(objectCode, objectClass, definition, collectible);
        if (objectInstance != null) container.Add(objectCode, objectInstance);
    }
}
