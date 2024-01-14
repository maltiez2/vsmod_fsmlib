using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Inputs;

public class Custom : BaseInput, ICustomInput
{
    public Custom(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        Code = code;
    }

    public string Code { get; private set; }

    public override string ToString() => $"Custom input: {Code}";
}
