using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Inputs;

public abstract class OperationInput : BaseInput, IOperationInput
{
    protected OperationInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        OperationCode = definition["operation"].AsString();
    }

    public IOperation Operation { get; set; }
    public string OperationCode { get; set; }
}


public class OperationStarted : OperationInput, IOperationStarted
{
    public OperationStarted(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }
}

public class OperationFinished : OperationInput, IOperationFinished
{
    public OperationFinished(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }
}