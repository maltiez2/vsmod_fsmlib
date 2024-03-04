using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace MaltiezFSM.Inputs;

public class ToolMode : BaseInput, IToolModeInput
{  
    public ToolMode(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        ModeId = definition["modeId"].AsString("");
    }

    public string ModeId { get; }

    public override string ToString() => $"Tool mode input: '{ModeId}'";
}
