using MaltiezFSM.API;
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
    public BeforeSlotChanged(ICoreAPI api, string code, CollectibleObject collectible, ISlotInput.SlotEventType eventType = ISlotInput.SlotEventType.FromSlot, BaseInputProperties? baseProperties = null) : base(api, code, collectible, baseProperties)
    {
        EventType = eventType;
    }

    public override string ToString() => $"Before slot changed: {EventType}";
}

public class AfterSlotChanged : BaseInput, ISlotChangedAfter
{
    public ISlotInput.SlotEventType EventType { get; private set; }

    public AfterSlotChanged(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        EventType = (ISlotInput.SlotEventType)Enum.Parse(typeof(ISlotInput.SlotEventType), definition["type"].AsString("ToSlot"));
    }
    public AfterSlotChanged(ICoreAPI api, string code, CollectibleObject collectible, ISlotInput.SlotEventType eventType = ISlotInput.SlotEventType.ToSlot, BaseInputProperties? baseProperties = null) : base(api, code, collectible, baseProperties)
    {
        EventType = eventType;
    }

    public override string ToString() => $"After slot changed: {EventType}";
}
