using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

namespace MaltiezFSM.Framework
{
    public class BehaviourAttributesParser : IBehaviourAttributesParser
    {
        private readonly Dictionary<string, IOperation> mOperations = new();
        private readonly Dictionary<string, ISystem> mSystems = new();
        private readonly Dictionary<string, IInput> mInputs = new();

        bool IBehaviourAttributesParser.ParseDefinition(IFactory<IOperation> operationTypes, IFactory<ISystem> systemTypes, IFactory<IInput> inputTypes, JsonObject behaviourAttributes, CollectibleObject collectible)
        {
            foreach (JsonObject systemDefinition in behaviourAttributes["systems"].AsArray())
            {
                AddObject(systemDefinition, collectible, systemTypes, mSystems); 
            }

            foreach (JsonObject systemDefinition in behaviourAttributes["operations"].AsArray())
            {
                AddObject(systemDefinition, collectible, operationTypes, mOperations);
            }

            foreach (JsonObject systemDefinition in behaviourAttributes["inputs"].AsArray())
            {
                AddObject(systemDefinition, collectible, inputTypes, mInputs);
            }

            return true;
        }
        Dictionary<string, IOperation> IBehaviourAttributesParser.GetOperations() => mOperations;
        Dictionary<string, ISystem> IBehaviourAttributesParser.GetSystems() => mSystems;
        Dictionary<string, IInput> IBehaviourAttributesParser.GetInputs() => mInputs;

        static private void AddObject<TObjectInterface>(JsonObject definition, CollectibleObject collectible, IFactory<TObjectInterface> factory, Dictionary<string, TObjectInterface> container)
        {
            string objectCode = definition["code"].AsString();
            string objectClass = definition["class"].AsString();
            TObjectInterface objectInstance = factory.Instantiate(objectCode, objectClass, definition, collectible);
            if (objectInstance != null) container.Add(objectCode, objectInstance);
        }
    }
}
