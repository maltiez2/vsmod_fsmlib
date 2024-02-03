using AnimationManagerLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems;

public class CameraSettings : BaseSystem
{
    private readonly AnimationManagerLibSystem mSettingsSystem;

    public CameraSettings(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mSettingsSystem = mApi.ModLoader.GetModSystem<AnimationManagerLibSystem>();
    }

    public override bool Process(ItemSlot slot, IPlayer player, JsonObject parameters)
    {
        if (!base.Process(slot, player, parameters)) return false;

        string action = parameters["action"].AsString("set");
        float blendingSpeed = parameters["speed"].AsFloat(1.0f);
        float value = parameters["value"].AsFloat(1.0f);

        CameraSettingsType setting;
        try
        {
            setting = (CameraSettingsType)Enum.Parse(typeof(CameraSettingsType), parameters["setting"].AsString(null));
        }
        catch
        {
            LogError($"Camera setting '{parameters["setting"].AsString("<not specified>")}' not found");
            return false;
        }

        switch (action)
        {
            case "set":
                if (mApi.Side != EnumAppSide.Client) return true;
                mSettingsSystem.SetCameraSetting("fsmlib", setting, value, blendingSpeed);
                break;
            case "reset":
                if (mApi.Side != EnumAppSide.Client) return true;
                mSettingsSystem.ResetCameraSetting("fsmlib", setting, blendingSpeed);
                break;
            default:
                LogActions(action, "set", "reset");
                return false;
        }
        return true;
    }
}
