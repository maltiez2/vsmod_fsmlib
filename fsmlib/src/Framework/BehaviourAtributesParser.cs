using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using Newtonsoft.Json.Linq;

namespace MaltiezFSM.Framework
{
    public class BehaviourAttributesParser : IBehaviourAttributesParser
    {
        private readonly Dictionary<string, IOperation> mOperations = new();
        private readonly Dictionary<string, ISystem> mSystems = new();
        private readonly Dictionary<string, IInput> mInputs = new();

        bool IBehaviourAttributesParser.ParseDefinition(IFactory<IOperation> operationTypes, IFactory<ISystem> systemTypes, IFactory<IInput> inputTypes, JsonObject behaviourAttributes, CollectibleObject collectible)
        {
            foreach ((string code, JToken definition) in (behaviourAttributes["systems"].Token as JObject))
            {
                AddObject(code, new JsonObject(definition), collectible, systemTypes, mSystems);
            }

            foreach ((string code, JToken definition) in (behaviourAttributes["operations"].Token as JObject))
            {
                AddObject(code, new JsonObject(definition), collectible, operationTypes, mOperations);
            }

            foreach ((string code, JToken definition) in (behaviourAttributes["inputs"].Token as JObject))
            {
                AddObject(code, new JsonObject(definition), collectible, inputTypes, mInputs);
            }

            return true;
        }
        Dictionary<string, IOperation> IBehaviourAttributesParser.GetOperations() => mOperations;
        Dictionary<string, ISystem> IBehaviourAttributesParser.GetSystems() => mSystems;
        Dictionary<string, IInput> IBehaviourAttributesParser.GetInputs() => mInputs;

        static private void AddObject<TObjectInterface>(string objectCode, JsonObject definition, CollectibleObject collectible, IFactory<TObjectInterface> factory, Dictionary<string, TObjectInterface> container)
        {
            string objectClass = definition["class"].AsString();
            TObjectInterface objectInstance = factory.Instantiate(objectCode, objectClass, definition, collectible);
            if (objectInstance != null) container.Add(objectCode, objectInstance);
        }
    }
}
