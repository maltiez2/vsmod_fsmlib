using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using static MaltiezFSM.API.IInput;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace MaltiezFSM.Inputs
{
    public class BaseInput : UniqueIdFactoryObject, IInput
    {
        public const string handledAttrName = "handle";
        public const string slotAttrName = "slot";

        public readonly Dictionary<string, SlotTypes> slotTypes = new Dictionary<string, IInput.SlotTypes>
        {
            {"mainhand", SlotTypes.MAIN_HAND},
            {"offhand", SlotTypes.OFF_HAND},
            {"any", SlotTypes.ANY},
            {"all", SlotTypes.ALL}
        };

        private string mCode;
        private bool mHandled;
        private SlotTypes mSlotType;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mHandled = definition == null ? true : definition[handledAttrName].AsBool(true);
            mSlotType = definition == null ? SlotTypes.MAIN_HAND : slotTypes[definition[slotAttrName].AsString("mainhand")];
        }

        public string GetName()
        {
            return mCode;
        }
        public bool Handled()
        {
            return mHandled;
        }

        public SlotTypes SlotType()
        {
            return mSlotType;
        }

        public virtual WorldInteraction GetInteractionInfo(ItemSlot slot)
        {
            return null;
        }
    }
}

