using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    internal class BasicPlayerAnimation : BaseSystem
    {
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string code = parameters["code"].AsString();
            string type = parameters["type"].AsString();
            switch (type)
            {
                case "start":
                    player.AnimManager.StartAnimation(code);
                    break;
                case "stop":
                    player.AnimManager.StopAnimation(code);
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [BasicPlayerAnimation] [Process] Type does not exists: " + type);
                    return false;
            }
            return true;
        }
    }
}
