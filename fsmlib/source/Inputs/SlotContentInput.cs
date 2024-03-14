using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Inputs;

public class SlotContent : BaseInput, ISlotContentInput
{
    public SlotContent(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        EventType = (ISlotContentInput.SlotEventType)Enum.Parse(typeof(ISlotContentInput.SlotEventType), definition["type"].AsString("AllTaken"));
    }
    public SlotContent(ICoreAPI api, string code, CollectibleObject collectible, ISlotContentInput.SlotEventType eventType, BaseInputProperties? baseProperties = null) : base(api, code, collectible, baseProperties)
    {
        EventType = eventType;
    }

    public ISlotContentInput.SlotEventType EventType { get; }

    public override string ToString() => $"Slot content: {EventType}";
}
