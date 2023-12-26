using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

#nullable enable

namespace MaltiezFSM.Framework;

public sealed class OperationInputInvoker : IInputInvoker, IOperationInputInvoker
{
    private readonly Dictionary<IOperationInput, IInputInvoker.InputCallback> mOperationStartedInputs = new();
    private readonly Dictionary<IOperationInput, IInputInvoker.InputCallback> mOperationFinishedInputs = new();

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is IOperationStarted started)
        {
            mOperationStartedInputs.Add(started, callback);
        }
        else if (input is IOperationFinished finished)
        {
            mOperationFinishedInputs.Add(finished, callback);
        }
    }

    public bool Started(IOperation operation, ItemSlot inSlot, IPlayer player) => Invoke(operation, inSlot, player, mOperationStartedInputs);
    public bool Finished(IOperation operation, ItemSlot inSlot, IPlayer player) => Invoke(operation, inSlot, player, mOperationFinishedInputs);

    private static bool Invoke(IOperation operation, ItemSlot inSlot, IPlayer player, Dictionary<IOperationInput, IInputInvoker.InputCallback> inputs)
    {
        foreach ((IOperationInput input, IInputInvoker.InputCallback callback) in inputs.Where((entry, _) => entry.Key.Operation == operation))
        {
            Utils.SlotData slotData = new(input.SlotType(), inSlot, player);

            if (callback.Invoke(slotData, player, input)) return true;
        }

        return false;
    }

    public void Dispose()
    {
        
    }
}
