using MaltiezFSM.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Inputs
{
    public class ItemDropped : BaseInput, IItemDropped
    {
        public ItemDropped(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
        {
        }
    }
}
