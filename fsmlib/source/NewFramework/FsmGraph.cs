using MaltiezFSM.API;

namespace MaltiezFSM.NewFramework;

internal sealed class FsmGraph
{
    public Dictionary<IState, FsmGraphNode> Nodes { get; } = new();

    public FsmGraph()
    {

    }
}

internal sealed class FsmGraphNode
{
    public IState State { get; }
    public Dictionary<IInput, FsmGraphEdge> ForwardEdges { get; }

    public FsmGraphNode(IState state, Dictionary<IInput, FsmGraphEdge> forwardEdges, HashSet<FsmGraphEdge> backwardsEdges)
    {
        State = state;
        ForwardEdges = forwardEdges;
    }
}

internal sealed class FsmGraphEdge
{
    public IInput Trigger { get; }
    public List<IOperation> Operations { get; }
    public FsmGraphNode Tail { get; }
    public FsmGraphNode Head { get; }

    public FsmGraphEdge(IInput trigger, List<IOperation> operations, FsmGraphNode tail, FsmGraphNode head)
    {
        Trigger = trigger;
        Operations = operations;
        Tail = tail;
        Head = head;
    }
}