using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Operations
{
    public class Branched : Delayed
    {
        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mCode = code;
            mApi = api;

            List<TransitionsBranchInitial> transitions = ParseTransitions(definition);
            HashSet<string> finalStates = CollectFinalStates(transitions);

            ParseInputsToPrevent(definition);

            List<(string, JsonObject)> systemsInitial = ParseSystems(definition, "initial");
            List<(string, JsonObject)> systemsTimeout = ParseSystems(definition, "timeout");
            Dictionary<string, List<(string, JsonObject)>> systemsFinal = new();

            List<string> inputsInitial = ParseInputs(definition, "initial");
            Dictionary<string, List<string>> inputsFinal = new();

            foreach (string state in finalStates)
            {
                systemsFinal.Add(state, ParseSystems(definition, state));
                inputsFinal.Add(state, ParseInputs(definition, state));
            }

            int? timeout = definition.KeyExists(delayAttrName) ? definition[delayAttrName].AsInt() : null;
            
            foreach (TransitionsBranchInitial transition in transitions)
            {
                AddTransition(transition.initial, transition.intermediate, inputsInitial, systemsInitial);
                AddTransitionForTimeout(timeout, transition.intermediate, transition.timeout, inputsInitial, systemsTimeout);
                AddTransitionsForInputsToPrevent(transition.intermediate);

                foreach (string finalState in transition.final)
                {
                    AddTransition(transition.intermediate, finalState, inputsFinal[finalState], systemsFinal[finalState]);
                }
            }
        }

        private List<TransitionsBranchInitial> ParseTransitions(JsonObject definition)
        {
            List<TransitionsBranchInitial> transitions = new();

            JsonObject[] mainTransitions = definition["states"].AsArray();
            foreach (JsonObject transition in mainTransitions)
            {
                string initial = transition["initial"].AsString();
                string timeout = transition["timeout"].AsString();
                string intermediate = initial + "_to_" + timeout + "_op." + mCode;

                transitions.Add(new TransitionsBranchInitial(initial, intermediate, timeout, ParseFinalStates(transition)));
            }

            return transitions;
        }

        private string[] ParseFinalStates(JsonObject transition)
        {
            if (!transition["final"].IsArray())
            {
                return new string[] { transition["final"].AsString() }; 
            }

            List<string> finalStates = new();

            foreach (JsonObject state in transition["final"].AsArray())
            {
                finalStates.Add(state.AsString());
            }

            return finalStates.ToArray();
        }

        private HashSet<string> CollectFinalStates(List<TransitionsBranchInitial> transitions)
        {
            HashSet<string> states = new();

            foreach (TransitionsBranchInitial transition in transitions)
            {
                foreach (string finalState in transition.final.Where(state => !states.Contains(state)))
                {
                    states.Add(finalState);
                }
            }

            return states;
        }
    }
}
