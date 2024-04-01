using MaltiezFSM.API;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace MaltiezFSM.Inputs;

public class StatusInput : BaseInput, IStatusInput
{
    public StatusInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        Status = (IStatusInput.StatusType)Enum.Parse(typeof(IStatusInput.StatusType), definition["status"].AsString());
        InvertStatus = definition["invert"].AsBool(false);
    }
    public StatusInput(ICoreAPI api, string code, CollectibleObject collectible, IStatusInput.StatusType status, bool invert = false, BaseInputProperties? baseProperties = null) : base(api, code, collectible, baseProperties)
    {
        Status = status;
        InvertStatus = invert;
    }

    public bool InvertStatus { get; private set; }
    public IStatusInput.StatusType Status { get; private set; }

    public override string ToString()
    {
        return $"Status input: {Status} (invert: {InvertStatus})";
    }
}
