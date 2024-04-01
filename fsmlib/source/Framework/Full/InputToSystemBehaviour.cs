using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

public class InputToSystemBehaviour : FiniteStateMachineBehaviour
{
    public InputToSystemBehaviour(CollectibleObject collObj) : base(collObj)
    {
    }

    protected override bool ParseDefinition(
        ICoreAPI api,
        JsonObject behaviourAttributes,
        CollectibleObject collectible,
        out Dictionary<string, IOperation> operations,
        out Dictionary<string, ISystem> systems,
        out Dictionary<string, IInput> inputs
        )
    {
        FiniteStateMachineSystem modSystem = api.ModLoader.GetModSystem<FiniteStateMachineSystem>();

        IFactory<IOperation>? operationTypes = modSystem.GetOperationFactory();
        IFactory<ISystem>? systemTypes = modSystem.GetSystemFactory();
        IFactory<IInput>? inputTypes = modSystem.GetInputFactory();

        Dictionary<string, IOperation> operationsLocal = new();
        Dictionary<string, ISystem> systemsLocal = new();
        Dictionary<string, IInput> inputsLocal = new();

        operations = new();
        systems = new();
        inputs = new();

        if (systemTypes == null || operationTypes == null || inputTypes == null) return false;

        try
        {
            Utils.Iterate(behaviourAttributes["systems"], (code, definition) => AddObject(api, "System", code, definition, collectible, systemTypes, systemsLocal));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating a system for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating a system for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        try
        {
            Utils.Iterate(behaviourAttributes["operations"], (code, definition) => AddObject(api, "Operation", code, definition, collectible, operationTypes, operationsLocal));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating an operation for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating an operation for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        try
        {
            Utils.Iterate(behaviourAttributes["inputs"], (code, definition) => AddObject(api, "Input", code, definition, collectible, inputTypes, inputsLocal));
        }
        catch (Exception exception)
        {
            Logger.Error(api, this, $"Exception on instantiating an input for '{collectible.Code}'");
            Logger.Debug(api, this, $"Exception on instantiating an input for '{collectible.Code}'.\nException:\n{exception}");
            return false;
        }

        operations = operationsLocal;
        systems = systemsLocal;
        inputs = inputsLocal;

        return true;
    }
}
