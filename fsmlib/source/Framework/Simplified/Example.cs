using MaltiezFSM.API;
using MaltiezFSM.Inputs;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework.Simplified;

public class BaseItemInteractions
{
    public BaseItemInteractions(ICoreAPI api, CollectibleObject collectible)
    {
        StartAttack = new(api, "attackStart", collectible, EnumMouseButton.Left, new() { EventType = IMouseInput.MouseEventType.MouseDown });
        CancelAttack = new(api, "attackCancel", collectible, EnumMouseButton.Left, new() { EventType = IMouseInput.MouseEventType.MouseUp });
        StartInteract = new(api, "interactStart", collectible, EnumMouseButton.Right, new() { EventType = IMouseInput.MouseEventType.MouseDown });
        CancelInteract = new(api, "interactCancel", collectible, EnumMouseButton.Right, new() { EventType = IMouseInput.MouseEventType.MouseUp });
        ItemDropped = new(api, "dropped", collectible, ISlotContentInput.SlotEventType.AllTaken);
        SlotDeselected = new(api, "deselected", collectible);

        Fsm = new FiniteStateMachineAttributesBased(api, new() { new() { "idle", "interacting", "attacking" } }, "idle");
        Fsm.Init(this, collectible);
    }

    protected IFiniteStateMachineAttributesBased Fsm;

    [Input]
    protected MouseKey StartAttack { get; }
    [Input]
    protected MouseKey CancelAttack { get; }
    [Input]
    protected MouseKey StartInteract { get; }
    [Input]
    protected MouseKey CancelInteract { get; }

    [Input]
    protected SlotContent ItemDropped { get; }
    [Input]
    protected BeforeSlotChanged SlotDeselected { get; }

    [InputHandler(state: "idle", "Attack")]
    protected bool OnAttack(ItemSlot slot, IPlayer? player, IInput input, IState state)
    {
        if (OnAttackStart(slot, player))
        {
            Fsm.SetState(slot, "attacking");
            return true;
        }

        return false;
    }
    protected virtual bool OnAttackStart(ItemSlot slot, IPlayer? player)
    {
        return false;
    }

    [InputHandler(state: "attacking", "CancelAttack")]
    protected bool OnAttackCancel(ItemSlot slot, IPlayer? player, IInput input, IState state)
    {
        if (OnAttackCancel(slot, player))
        {
            Fsm.SetState(slot, "idle");
            return false;
        }

        return true;
    }
    protected virtual bool OnAttackCancel(ItemSlot slot, IPlayer? player)
    {
        return false;
    }

    [InputHandler(state: "idle", "StartInteract")]
    protected bool OnInteractStart(ItemSlot slot, IPlayer? player, IInput input, IState state)
    {
        if (OnInteractStart(slot, player))
        {
            Fsm.SetState(slot, "interacting");
            return true;
        }

        return false;
    }
    protected virtual bool OnInteractStart(ItemSlot slot, IPlayer? player)
    {
        return false;
    }

    [InputHandler(state: "interacting", "CancelInteract", "ItemDropped", "SlotDeselected")]
    protected bool OnInteractCancel(ItemSlot slot, IPlayer? player, IInput input, IState state)
    {
        if (OnInteractCancel(slot, player))
        {
            Fsm.SetState(slot, "idle");
            return false;
        }

        return true;
    }
    protected virtual bool OnInteractCancel(ItemSlot slot, IPlayer? player)
    {
        return false;
    }
}