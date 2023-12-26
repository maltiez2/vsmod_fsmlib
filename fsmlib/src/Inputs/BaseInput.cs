using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;
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
        private Utils.SlotType mSlotType;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            if (definition == null)
            {
                Utils.Logger.Error(api, this, $"Input '{code}' got empty definition");
                return;
            }

            mCollectible = collectible;
            mCode = code;
            mApi = api;
            mHandled = definition["handle"].AsBool(true);
            mSlotType = (Utils.SlotType)Enum.Parse(typeof(Utils.SlotType), definition["slot"].AsString("mainHand"));
        }

        public string GetName() => mCode;
        public Utils.SlotType SlotType() => mSlotType;
        public int Index { get; set; }

        public virtual bool Handled() => mHandled;
        public virtual WorldInteraction GetInteractionInfo(ItemSlot slot) => null;
    }
}

