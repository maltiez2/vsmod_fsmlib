using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
using static MaltiezFSM.API.IInput;
using System.Collections.Generic;
using Vintagestory.API.Client;
using System;
using MaltiezFSM.Framework;

namespace MaltiezFSM.Inputs
{
    public class BaseInput : UniqueId, IInput
    {
        protected ICoreAPI mApi;
        protected CollectibleObject mCollectible;
        
        private string mCode;
        private bool mHandled;
        private SlotTypes mSlotType;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            if (definition == null)
            {
                Utils.Logger.Error(this, "Input '{0}' got empty definition");
                return;
            }

            mCollectible = collectible;
            mCode = code;
            mApi = api;
            mHandled = definition["handle"].AsBool(true);
            mSlotType = (SlotTypes)Enum.Parse(typeof(SlotTypes), definition["slot"].AsString("mainHand"));
        }

        string IInput.GetName() => mCode;
        SlotTypes IInput.SlotType() => mSlotType;

        public virtual bool Handled() => mHandled;
        public virtual WorldInteraction GetInteractionInfo(ItemSlot slot) => null;
    }
}

