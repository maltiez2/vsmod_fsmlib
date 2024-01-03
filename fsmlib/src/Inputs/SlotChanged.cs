using MaltiezFSM.API;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Inputs;

public class BeforeSlotChanged : BaseInput, ISlotChangedBefore
{
    public EnumHandling Handling => Handle ? EnumHandling.PreventSubsequent : EnumHandling.Handled;

    public ISlotInput.SlotEventType EventType { get; private set; }

    public BeforeSlotChanged(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        EventType = (ISlotInput.SlotEventType)Enum.Parse(typeof(ISlotInput.SlotEventType), definition["type"].AsString("FromSlot"));
    }
}

public class AfterSlotChanged : BaseInput, ISlotChangedAfter
{
    public ISlotInput.SlotEventType EventType { get; private set; }

    public AfterSlotChanged(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        EventType = (ISlotInput.SlotEventType)Enum.Parse(typeof(ISlotInput.SlotEventType), definition["type"].AsString("FromSlot"));
    }
}
