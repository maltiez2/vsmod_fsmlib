using MaltiezFSM.API;
using Vintagestory.API.Common;

namespace MaltiezFSM.NewFramework;

internal sealed class FsmGraph
{
    public Dictionary<IState, FsmGraphNode> Nodes { get; } = new();

    public FsmGraph(IEnumerable<ITransition> transitions)
    {
        CreateNodes(transitions);
        CreateEdges(transitions);
    }

    public IState Process(IState state, IInput input, ItemSlot slot, IPlayer player)
    {
        if (!Nodes.ContainsKey(state)) return state;

        return Nodes[state].Process(input, slot, player);
    }

    private void CreateNodes(IEnumerable<ITransition> transitions)
    {
        foreach (ITransition transition in transitions)
        {
            IState head = transition.Head;
            IState tail = transition.Tail;

            if (!Nodes.ContainsKey(head)) Nodes.Add(head, new(head));
            if (!Nodes.ContainsKey(tail)) Nodes.Add(tail, new(tail));
        }
    }
    private void CreateEdges(IEnumerable<ITransition> transitions)
    {
        foreach ((IState state, FsmGraphNode node) in Nodes)
        {
            foreach (ITransition transition in transitions.Where(transition => transition.Tail == state))
            {
                CreateEdge(node, transition);
            }
        }
    }
    private void CreateEdge(FsmGraphNode node, ITransition transition)
    {
        if (node.Edges.ContainsKey(transition.Trigger))
        {
            node.Edges[transition.Trigger].Transitions.Add(transition.Process); // @TODO add checks for one input+state leading to multiple states
            return;
        }

        node.Edges.Add(transition.Trigger, new(transition.Trigger, new() { transition.Process }, node, Nodes[transition.Head]));
    }
}

internal sealed class FsmGraphNode
{
    public IState State { get; }
    public Dictionary<IInput, FsmGraphEdge> Edges { get; } = new();

    public FsmGraphNode(IState state)
    {
        State = state;
    }

    public IState Process(IInput input, ItemSlot slot, IPlayer player)
    {
        if (Edges.ContainsKey(input) && Edges[input].Process(slot, player))
        {
            return Edges[input].Head.State;
        }

        return State;
    }
}

internal sealed class FsmGraphEdge
{
    public IInput Trigger { get; }
    public List<TransitionDelegate> Transitions { get; }
    public FsmGraphNode Tail { get; }
    public FsmGraphNode Head { get; }

    public FsmGraphEdge(IInput trigger, List<TransitionDelegate> transitions, FsmGraphNode tail, FsmGraphNode head)
    {
        Trigger = trigger;
        Transitions = transitions;
        Tail = tail;
        Head = head;
    }

    public bool Process(ItemSlot slot, IPlayer player)
    {
        foreach (TransitionDelegate transition in Transitions)
        {
            if (transition.Invoke(slot, player)) return true;
        }

        return false;
    }
}