using MaltiezFSM.API;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;



namespace MaltiezFSM.Framework;

public sealed class OperationInputInvoker : IInputInvoker, IOperationInputInvoker
{
    private readonly Dictionary<IOperationInput, IInputInvoker.InputCallback> mOperationStartedInputs = new();
    private readonly Dictionary<IOperationInput, IInputInvoker.InputCallback> mOperationFinishedInputs = new();
    private readonly Dictionary<IInput, CollectibleObject> mCollectibles = new();

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is IOperationStarted started)
        {
            mOperationStartedInputs.Add(started, callback);
            mCollectibles.Add(input, collectible);
        }
        else if (input is IOperationFinished finished)
        {
            mOperationFinishedInputs.Add(finished, callback);
            mCollectibles.Add(input, collectible);
        }
    }

    public bool Started(IOperation operation, ItemSlot inSlot, IPlayer player) => Invoke(operation, inSlot, player, mOperationStartedInputs);
    public bool Finished(IOperation operation, ItemSlot inSlot, IPlayer player) => Invoke(operation, inSlot, player, mOperationFinishedInputs);

    private bool Invoke(IOperation operation, ItemSlot inSlot, IPlayer player, Dictionary<IOperationInput, IInputInvoker.InputCallback> inputs)
    {
        foreach ((IOperationInput input, IInputInvoker.InputCallback callback) in inputs.Where((entry, _) => entry.Key.Operation == operation))
        {
            if (inSlot?.Itemstack?.Collectible != mCollectibles[input]) continue;

            Utils.SlotData slotData = new(input.Slot, inSlot, player);

            if (callback.Invoke(slotData, player, input)) return true;
        }

        return false;
    }

    public void Dispose()
    {

    }
}
