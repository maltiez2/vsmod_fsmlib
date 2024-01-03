using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

public class BehaviourAttributesParser : IBehaviourAttributesParser
{
    private readonly Dictionary<string, IOperation> mOperations = new();
    private readonly Dictionary<string, ISystem> mSystems = new();
    private readonly Dictionary<string, IInput> mInputs = new();

    public bool ParseDefinition(IFactory<IOperation>? operationTypes, IFactory<ISystem>? systemTypes, IFactory<IInput>? inputTypes, JsonObject behaviourAttributes, CollectibleObject collectible)
    {
        if (systemTypes != null) Utils.Iterate(behaviourAttributes["systems"], (code, definition) => AddObject(code, definition, collectible, systemTypes, mSystems));
        if (operationTypes != null) Utils.Iterate(behaviourAttributes["operations"], (code, definition) => AddObject(code, definition, collectible, operationTypes, mOperations));
        if (inputTypes != null) Utils.Iterate(behaviourAttributes["inputs"], (code, definition) => AddObject(code, definition, collectible, inputTypes, mInputs));

        return true;
    }
    public Dictionary<string, IOperation> GetOperations() => mOperations;
    public Dictionary<string, ISystem> GetSystems() => mSystems;
    public Dictionary<string, IInput> GetInputs() => mInputs;

    static private void AddObject<TObjectInterface>(string objectCode, JsonObject definition, CollectibleObject collectible, IFactory<TObjectInterface> factory, Dictionary<string, TObjectInterface> container)
    {
        string objectClass = definition["class"].AsString();
        TObjectInterface? objectInstance = factory.Instantiate(objectCode, objectClass, definition, collectible);
        if (objectInstance != null) container.Add(objectCode, objectInstance);
    }
}
