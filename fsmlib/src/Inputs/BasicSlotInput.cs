using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using System.Collections.Generic;

namespace MaltiezFSM.Inputs
{
    public class SlotModified : BaseInput, ISlotModified
    {

    }
    
    public class BasicSlotBefore : BaseInput, ISlotChangedBefore
    {
        private readonly Dictionary<string, EnumHandling> mTypes = new Dictionary<string, EnumHandling>
        {
            { "prevent",    EnumHandling.PreventSubsequent },
            { "handle",     EnumHandling.Handled }
        };
        private EnumHandling mInputType;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            mInputType = mTypes[definition["type"].AsString()];
        }

        public EnumHandling GetHandlingType()
        {
            return mInputType;
        }
    }

    public class BasicSlotAfter : BaseInput, ISlotChangedAfter
    {
        public ISlotChangedAfter.SlotEventType GetEventType()
        {
            return ISlotChangedAfter.SlotEventType.TO_WEAPON;
        }
    }
}

