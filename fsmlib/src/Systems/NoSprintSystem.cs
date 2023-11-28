using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    public class NoSprint : BaseSystem
    {
        public override bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            if (!base.Process(slot, player, parameters)) return false;

            string action = parameters["action"].AsString();
            switch (action)
            {
                case "forbid":
                    player.Controls.Sprint = false;
                    player.ServerControls.Sprint = false;
                    break;
                case "allow":
                    player.Controls.Sprint = true;
                    player.ServerControls.Sprint = true;
                    break;
                default:
                    mApi.Logger.Error("[FSMlib] [NoSprint] [Process] Action does not exists: " + action);
                    return false;
            }
            return true;
        }
    }
}
