using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    public class BaseSystem : UniqueIdFactoryObject, ISystem
    {
        protected ICoreAPI mApi;
        protected CollectibleObject mCollectible;
        protected string mCode;

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mApi = api;
            mCode = code;
            mCollectible = collectible;

            if (definition == null) mApi?.Logger.Error("[FSMlib] [Init] System '" + mCode + "' received 'null' definition");
            if (collectible == null) mApi?.Logger.Error("[FSMlib] [Init] System '" + mCode + "' received 'null' collectible");
        }

        virtual public string[] GetDescription(ItemSlot slot, IWorldAccessor world)
        {
            if (slot == null)
            {
                mApi?.Logger.Warning("[FSMlib] [GetDescription] System '" + mCode + "' received 'null' slot");
            }

            return System.Array.Empty<string>();
        }

        virtual public bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (slot  == null)
            {
                mApi?.Logger.Error("[FSMlib] [Process] System '" + mCode + "' received 'null' slot");
                return false;
            }

            if (player == null)
            {
                mApi?.Logger.Error("[FSMlib] [Process] System '" + mCode + "' received 'null' player");
                return false;
            }

            if (parameters == null)
            {
                mApi?.Logger.Error("[FSMlib] [Process] System '" + mCode + "' received 'null' parameters");
                return false;
            }

            return true;
        }

        virtual public void SetSystems(Dictionary<string, ISystem> systems)
        {
            
        }

        virtual public bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (slot == null)
            {
                mApi?.Logger.Error("[FSMlib] [Verify] System '" + mCode + "' received 'null' slot");
                return false;
            }

            if (player == null)
            {
                mApi?.Logger.Error("[FSMlib] [Verify] System '" + mCode + "' received 'null' player");
                return false;
            }

            if (parameters == null)
            {
                mApi?.Logger.Error("[FSMlib] [Verify] System '" + mCode + "' received 'null' parameters");
                return false;
            }

            return true;
        }
    }
}
