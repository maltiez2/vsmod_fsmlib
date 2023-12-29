using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;

#nullable enable

namespace MaltiezFSM.Framework;

public sealed class CustomInputInvoker : IInputInvoker, ICustomInputInvoker
{
    private readonly Dictionary<ICustomInput, IInputInvoker.InputCallback> mInputs = new();
    private readonly Dictionary<string, List<ICustomInput>> mInputsByCode = new();

    public void RegisterInput(IInput input, IInputInvoker.InputCallback callback, CollectibleObject collectible)
    {
        if (input is ICustomInput custom)
        {
            mInputs.Add(custom, callback);
            mInputsByCode.TryAdd(custom.Code, new());
            mInputsByCode[custom.Code].Add(custom);
        }
    }

    public void Dispose()
    {

    }

    public bool Invoke(string input, IPlayer player, ItemSlot? inSlot = null)
    {
        if (!mInputsByCode.ContainsKey(input)) return false;

        foreach (ICustomInput registered in mInputsByCode[input])
        {
            if (mInputs[registered].Invoke(new(registered.Slot, inSlot, player), player, registered))
            {
                return true;
            }
        }

        return false;
    }
}
