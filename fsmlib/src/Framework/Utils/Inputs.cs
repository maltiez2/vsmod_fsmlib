using MaltiezFSM.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

internal static class InputsUtils
{
    public static bool TestStatus(IStatusModifier input, IPlayer player)
    {
        if (player.Entity == null || player.Entity.World == null) return true;
        
        switch (input.StatusCheckType)
        {
            case IStandardInput.MultipleCheckType.AtLeastOne:
                foreach (IStatusModifier.StatusType status in input.Statuses)
                {
                    if (CheckStatus(status, player)) return true;
                }
                return false;
            case IStandardInput.MultipleCheckType.AtLeastNotOne:
                foreach (IStatusModifier.StatusType status in input.Statuses)
                {
                    if (!CheckStatus(status, player)) return true;
                }
                return false;
            case IStandardInput.MultipleCheckType.All:
                foreach (IStatusModifier.StatusType status in input.Statuses)
                {
                    if (!CheckStatus(status, player)) return false;
                }
                return true;
            case IStandardInput.MultipleCheckType.None:
                foreach (IStatusModifier.StatusType status in input.Statuses)
                {
                    if (CheckStatus(status, player)) return false;
                }
                return true;
        }

        return false;
    }
    private static bool CheckStatus(IStatusModifier.StatusType status, IPlayer player)
    {
        return status switch
        {
            IStatusModifier.StatusType.Swimming => player.Entity.Swimming,
            IStatusModifier.StatusType.OnFire => player.Entity.IsOnFire,
            IStatusModifier.StatusType.Collided => player.Entity.Collided,
            IStatusModifier.StatusType.CollidedHorizontally => player.Entity.CollidedHorizontally,
            IStatusModifier.StatusType.CollidedVertically => player.Entity.CollidedVertically,
            IStatusModifier.StatusType.EyesSubmerged => player.Entity.IsEyesSubmerged(),
            IStatusModifier.StatusType.FeetInLiquid => player.Entity.FeetInLiquid,
            IStatusModifier.StatusType.InLava => player.Entity.InLava,
            IStatusModifier.StatusType.OnGround => player.Entity.OnGround,
            _ => false,
        };
    }
    public static bool TestActivities(IActivityModifier input, IPlayer player)
    {
        if (player.Entity == null || player.Entity.World == null) return true;

        switch (input.ActivityCheckType)
        {
            case IStandardInput.MultipleCheckType.AtLeastOne:
                foreach (string activity in input.Activities)
                {
                    if (player.Entity.IsActivityRunning(activity)) return true;
                }
                return false;
            case IStandardInput.MultipleCheckType.AtLeastNotOne:
                foreach (string activity in input.Activities)
                {
                    if (!player.Entity.IsActivityRunning(activity)) return true;
                }
                return false;
            case IStandardInput.MultipleCheckType.All:
                foreach (string activity in input.Activities)
                {
                    if (!player.Entity.IsActivityRunning(activity)) return false;
                }
                return true;
            case IStandardInput.MultipleCheckType.None:
                foreach (string activity in input.Activities)
                {
                    if (player.Entity.IsActivityRunning(activity)) return false;
                }
                return true;
        }

        return false;
    }
    public static bool TestModifiers(IKeyModifier input, IActionInputInvoker invoker, ICoreClientAPI api)
    {
        bool altPressed = TestModifier(EnumModifierKey.ALT, invoker, api);
        bool ctrlPressed = TestModifier(EnumModifierKey.CTRL, invoker, api);
        bool shiftPressed = TestModifier(EnumModifierKey.SHIFT, invoker, api);
        bool altShould = (input.ModifierKey & EnumModifierKey.ALT) == EnumModifierKey.ALT;
        bool ctrlShould = (input.ModifierKey & EnumModifierKey.CTRL) == EnumModifierKey.ALT;
        bool shiftShould = (input.ModifierKey & EnumModifierKey.SHIFT) == EnumModifierKey.ALT;
        
        bool altSuffice = !(altPressed ^ altShould);
        bool ctrlSuffice = !(ctrlPressed ^ ctrlShould);
        bool shiftSuffice = !(shiftPressed ^ shiftShould);

        bool present = (altPressed && altShould) || (ctrlPressed && ctrlShould) || (shiftPressed && shiftShould);

        return input.ModifierType switch
        {
            IKeyModifier.KeyModifierType.Strict => altSuffice && ctrlSuffice && shiftSuffice,
            IKeyModifier.KeyModifierType.Present => present,
            IKeyModifier.KeyModifierType.NotPresent => !present,
            _ => false,
        };
    }
    private static bool TestModifier(EnumModifierKey modifier, IActionInputInvoker invoker, ICoreClientAPI api)
    {
        EnumEntityAction action = modifier switch
        {
            EnumModifierKey.CTRL => EnumEntityAction.CtrlKey,
            EnumModifierKey.SHIFT => EnumEntityAction.ShiftKey,
            EnumModifierKey.ALT => EnumEntityAction.None,
            _ => EnumEntityAction.None
        };

        if (action == EnumEntityAction.None)
        {
            return api.Input.KeyboardKeyState[(int)GlKeys.AltLeft] || api.Input.KeyboardKeyState[(int)GlKeys.AltRight];
        }

        return invoker.IsActive(action, true);
    }
}
