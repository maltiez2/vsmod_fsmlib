using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace MaltiezFSM.Framework;

internal sealed class StateResolver
{
    public StateResolver(JsonObject states)
    {
        JsonObject[] statesElements = states.AsArray();
        StateDimension = statesElements.Length;
        foreach (JsonObject stateElement in statesElements)
        {
            _stateElementsVariants.Add(new());
            foreach (JsonObject element in statesElements)
            {
                _stateElementsVariants[^1].Add(element.AsString());
            }
        }

        ConstructStates();
    }

    public int StateDimension { get; }

    public IEnumerable<string> ResolveStates(string wildcard)
    {
        return _states.Where(state => WildcardUtil.Match(wildcard, state));
    }

    public bool ResolveTransition(string startingStateWildcard, string finishStateWildcard, out Dictionary<string, string> transitions)
    {
        transitions = new();
        IEnumerable<string> startingStates = _states.Where(state => WildcardUtil.Match(startingStateWildcard, state));
        IEnumerable<string> finishStates = _states.Where(state => WildcardUtil.Match(finishStateWildcard, state));

        if (!ConstructFinishStateMatcher(finishStates, out List<string> finishStateMatcher)) return false;
        
        foreach (string startingState in startingStates)
        {
            IEnumerable<string> transitionFinishStates = FindTransitionFinishStates(startingState, finishStateMatcher, finishStates);

            if (!transitionFinishStates.Any()) continue;
            if (transitionFinishStates.Count() > 1) return false;

            transitions.Add(startingState, transitionFinishStates.First());
        }

        return true;
    }

    public static bool MergeTransitions(out Dictionary<string, string> merged, params Dictionary<string, string>[] transitions)
    {
        merged = new();
        foreach (Dictionary<string, string> transitionBatch in transitions)
        {
            foreach ((string start, string finish) in transitionBatch)
            {
                if (!merged.ContainsKey(start))
                {
                    merged.Add(start, finish);
                    continue;
                }

                if (merged[start] != finish)
                {
                    return false;
                }
            }
        }

        return true;
    }


    private readonly List<HashSet<string>> _stateElementsVariants = new();
    private readonly HashSet<string> _states = new();

    private void ConstructStates(string state = "", int elementIndex = 0)
    {
        if (_stateElementsVariants.Count <= elementIndex)
        {
            _states.Add(state);
            return;
        }

        foreach (string elementValue in _stateElementsVariants[elementIndex])
        {
            ConstructStates(state + "-" + elementValue, elementIndex + 1);
        }
    }
    private bool ConstructFinishStateMatcher(IEnumerable<string> finishStates, out List<string> finishStateMatcher)
    {
        finishStateMatcher = new(StateDimension);

        for (int index = 0; index < StateDimension; index++)
        {
            finishStateMatcher.Add("");
        }

        foreach (string[] elements in finishStates.Select(state => state.Split("-")))
        {
            if (elements.Length != StateDimension) return false;
            for (int index = 0; index < elements.Length; index++)
            {
                if (finishStateMatcher[index] == "")
                {
                    finishStateMatcher[index] = elements[index];
                }
                else if (finishStateMatcher[index] != elements[index])
                {
                    finishStateMatcher[index] = "*";
                }
            }
        }

        return true;
    }
    private static IEnumerable<string> FindTransitionFinishStates(string startingState, List<string> finishStateMatcher, IEnumerable<string> finishStates)
    {
        string[] startingStateElements = startingState.Split("-");

        string matcher = finishStateMatcher
            .Select((finishStateElement, index) => finishStateElement == "*" ? startingStateElements[index] : finishStateElement)
            .Aggregate((first, second) => $"{first}-{second}");

        return finishStates.Where(state => WildcardUtil.Match(matcher, state));
    }
}
