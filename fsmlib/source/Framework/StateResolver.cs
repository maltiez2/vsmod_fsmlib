using MaltiezFSM.API;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace MaltiezFSM.Framework;

/// <summary>
/// Manages vector states consistency. Resolves states by wildcards matchers.<br/>
/// Vector state consists of N strings separated by delimiter "-", where N is defined on resolver construction and is fixed for given fsm.
/// </summary>
public interface IStateResolver
{
    /// <summary>
    /// Returns all states that matches this wildcard
    /// </summary>
    /// <param name="wildcard">State or wildcard expression to match existing states</param>
    /// <returns>All existing states that matches given wildcard</returns>
    public IEnumerable<string> ResolveStates(string wildcard);
    /// <summary>
    /// Constructs all valid combinations of transitions that matches given wildcards.<br/>
    /// Each starting state should correspond to only one finish state. Will return false otherwise.<br/>
    /// Each matcher should have same dimension defined on resolver construction.<br/>
    /// It means that you should have matchers that can be split by delimiter "-" into same amount of elements.
    /// </summary>
    /// <param name="startingStateWildcard">Transition starting state matcher</param>
    /// <param name="finishStateWildcard">Transition finish state matcher</param>
    /// <param name="transitions">Pairs of start-finish states for all valid transitions</param>
    /// <returns>false if error occurred</returns>
    bool ResolveTransitions(string startingStateWildcard, string finishStateWildcard, out Dictionary<string, string> transitions);
}

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
internal sealed class StateResolver : IStateResolver
{
    [JsonProperty("states")]
    public List<HashSet<string>> States { get; set; } = new();

    public IEnumerable<string> ResolveStates(string wildcard)
    {
        return _states.Where(state => WildcardUtil.Match(wildcard, state));
    }
    public bool ResolveTransitions(string startingStateWildcard, string finishStateWildcard, out Dictionary<string, string> transitions)
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

    public void Initialize()
    {
        _stateDimension = States.Count;
        ConstructStates();
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

    private readonly HashSet<string> _states = new();
    private int _stateDimension;

    [OnDeserialized]
    private void Initialize(StreamingContext context)
    {
        _stateDimension = States.Count;
        ConstructStates();
    }
    
    private void ConstructStates(string state = "", int elementIndex = 0)
    {
        if (_stateDimension <= elementIndex)
        {
            _states.Add(state);
            return;
        }

        if (state == "")
        {
            foreach (string elementValue in States[elementIndex])
            {
                ConstructStates(elementValue, elementIndex + 1);
            }
        }
        else
        {
            foreach (string elementValue in States[elementIndex])
            {
                ConstructStates(state + "-" + elementValue, elementIndex + 1);
            }
        }
    }
    private bool ConstructFinishStateMatcher(IEnumerable<string> finishStates, out List<string> finishStateMatcher)
    {
        finishStateMatcher = new(_stateDimension);

        for (int index = 0; index < _stateDimension; index++)
        {
            finishStateMatcher.Add("");
        }

        foreach (string[] elements in finishStates.Select(state => state.Split("-")))
        {
            if (elements.Length != _stateDimension) return false;
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
