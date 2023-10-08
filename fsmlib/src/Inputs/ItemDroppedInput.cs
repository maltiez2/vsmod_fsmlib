using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

namespace MaltiezFSM.Inputs
{
    public class ItemDropped : BaseInput, ISlotEvent
    {
        IActiveSlotListener.SlotEventType ISlotEvent.GetEventType()
        {
            return IActiveSlotListener.SlotEventType.ItemDropped;
        }
    }
}
