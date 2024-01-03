using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Inputs;

public class BaseInput : FactoryProduct, IInput
{
    public int Index { get; set; }
    public bool Handle => mHandled;
    public Utils.SlotType Slot => mSlotType;
    public virtual WorldInteraction? GetInteractionInfo(ItemSlot slot) => null;

    private readonly bool mHandled;
    private readonly Utils.SlotType mSlotType;

    public BaseInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        if (definition == null)
        {
            LogError("Empty definition");
            return;
        }

        mHandled = definition["handle"].AsBool(true);
        mSlotType = (Utils.SlotType)Enum.Parse(typeof(Utils.SlotType), definition["slot"].AsString("MainHand"));
    }
}
