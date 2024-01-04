using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;



namespace MaltiezFSM.Systems;
public class PlayerAnimation : BaseSystem
{
    public PlayerAnimation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string? code = parameters["animation"].AsString();

        if (code == null)
        {
            LogError($"No 'animation' in system request");
            return false;
        }

        string action = parameters["action"].AsString("start");
        switch (action)
        {
            case "start":
                player.Entity.StartAnimation(code);
                break;
            case "stop":
                player.Entity.StopAnimation(code);
                break;
            default:
                LogActions(action, "start", "stop");
                return false;
        }
        return true;
    }
}
