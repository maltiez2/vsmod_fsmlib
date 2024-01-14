using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class NoSprint : BaseSystem
{
    public NoSprint(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("start");
        switch (action)
        {
            case "start":
                player.Entity.Controls.Sprint = false;
                player.Entity.ServerControls.Sprint = false;
                break;
            case "stop":
                player.Entity.Controls.Sprint = true;
                player.Entity.ServerControls.Sprint = true;
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }
        return true;
    }
}
