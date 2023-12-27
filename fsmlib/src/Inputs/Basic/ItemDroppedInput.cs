using MaltiezFSM.API;

namespace MaltiezFSM.Inputs
{
    public class ItemDropped : BaseInput, IItemDropped
    {
        IActiveSlotListener.SlotEventType IItemDropped.GetEventType()
        {
            return IActiveSlotListener.SlotEventType.ItemDropped;
        }
    }
}
