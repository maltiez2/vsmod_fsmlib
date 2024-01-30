using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using VSImGui;

namespace MaltiezFSM.Systems;

public class ChangeAttribute : BaseSystem
{
    public ChangeAttribute(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string? attribute = parameters["attribute"].AsString();
        string? type = parameters["type"].AsString();

        if (attribute == null)
        {
            LogError($"Attribute was not specified");
            return false;
        }

        if (type == null)
        {
            LogError($"Type was not specified");
            return false;
        }

        if (!parameters.KeyExists("value"))
        {
            LogError($"Value was not specified");
            return false;
        }

        switch (type)
        {
            case "bool":
                slot.Itemstack?.Attributes.SetBool(attribute, parameters["value"].AsBool());
                break;
            case "int":
                slot.Itemstack?.Attributes.SetInt(attribute, parameters["value"].AsInt());
                break;
            case "float":
                slot.Itemstack?.Attributes.SetFloat(attribute, parameters["value"].AsFloat());
                break;
            case "string":
                slot.Itemstack?.Attributes.SetString(attribute, parameters["value"].AsString());
                break;
            default:
                LogError($"Type '{type}' is not supported. Supported types: bool, int, float, string");
                return false;
        }

        slot.MarkDirty();
        return true;
    }

    int index = 0;
}