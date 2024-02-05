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
    public SlotType Slot => mSlotType;
    public virtual WorldInteraction? GetInteractionInfo(ItemSlot slot) => null;
    public CollectibleObject Collectible { get; }

    private readonly bool mHandled;
    private readonly SlotType mSlotType;

    public BaseInput(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        Collectible = collectible;

        if (definition == null)
        {
            LogError("Empty definition");
            return;
        }

        mHandled = definition["handle"].AsBool(true);
        mSlotType = (SlotType)Enum.Parse(typeof(SlotType), definition["slot"].AsString("MainHand"));
    }
}
