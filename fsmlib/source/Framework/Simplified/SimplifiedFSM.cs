using MaltiezFSM.API;
using MaltiezFSM.Inputs;
using System.Reflection;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public delegate bool InputHandlerDelegate(ItemSlot slot, IPlayer? player, IInput input, IState state);

public interface IFiniteStateMachine
{
    Dictionary<IState, Dictionary<IInput, InputHandlerDelegate>> Handlers { get; set; }

    event Action<ItemSlot, IState>? StateChanged;

    bool SetState(ItemSlot slot, IState state);
    bool SetState(ItemSlot slot, string state);
    IState GetState(ItemSlot slot);
    IState DeserializeState(string state);
    IEnumerable<IState> ResolverState(string wildcard);
}
public interface IFiniteStateMachineAttributesBased : IFiniteStateMachine
{
    bool Init(object owner, CollectibleObject collectible);
    bool SetState(ItemSlot slot, int elementIndex, string value);
    bool SetState(ItemSlot slot, params (int elementIndex, string value)[] elements);
}

public class FiniteStateMachineSimplified : IFiniteStateMachine
{
    public Dictionary<IState, Dictionary<IInput, InputHandlerDelegate>> Handlers { get; set; } = new();
    public bool ResetStateOnInvalid { get; set; } = true;

    public event Action<ItemSlot, IState>? StateChanged;

    public FiniteStateMachineSimplified(IStateManager stateManager, IStateResolver stateResolver)
    {
        StateManager = stateManager;
        StateResolver = stateResolver;
    }

    public IEnumerable<IInput> GetAvailableInputs(ItemSlot slot)
    {
        IState state = StateManager.Get(slot);
        if (Handlers.ContainsKey(state)) return Array.Empty<IInput>();
        return Handlers[state].Select(entry => entry.Key);
    }
    public bool Process(ItemSlot slot, IPlayer? player, IInput input)
    {
        IState state = StateManager.Get(slot);
        if (!Handlers.ContainsKey(state))
        {
            if (ResetStateOnInvalid) StateManager.Reset(slot);
            return false;
        }
        if (!Handlers[state].ContainsKey(input))
        {
            return false;
        }

        return Handlers[state][input].Invoke(slot, player, input, state);
    }
    public bool SetState(ItemSlot slot, IState state)
    {
        IState current = StateManager.Get(slot);
        if (current == state) return false;

        StateManager.Set(slot, state);
        StateChanged?.Invoke(slot, state);
        return true;
    }
    public bool SetState(ItemSlot slot, string state)
    {
        if (!state.Contains('*'))
        {
            return SetState(slot, StateManager.DeserializeState(state));
        }
        
        IState current = StateManager.Get(slot);
        string[] currentSplitted = current.Serialize().Split('-');
        string[] stateSplitted = state.Split('-');
        List<string> newStateSplitted = new();
        for (int index = 0; index < currentSplitted.Length; index++)
        {
            newStateSplitted.Add(stateSplitted[index] == "*" ? currentSplitted[index] : stateSplitted[index]);
        }
        string newStateSerialized = newStateSplitted.Aggregate((first, second) => $"{first}-{second}");
        IState newState = StateManager.DeserializeState(newStateSerialized);
        if (current ==  newState) return false;

        StateManager.Set(slot, newState);
        return true;
    }
    public IState GetState(ItemSlot slot)
    {
        return StateManager.Get(slot);
    }
    public IState DeserializeState(string state)
    {
        return StateManager.DeserializeState(state);
    }
    public IEnumerable<IState> ResolverState(string wildcard)
    {
        return StateResolver.ResolveStates(wildcard).Select(StateManager.DeserializeState);
    }

    protected readonly IStateManager StateManager;
    protected readonly IStateResolver StateResolver;
}

public sealed class FiniteStateMachineAttributesBased : FiniteStateMachineSimplified, IFiniteStateMachineAttributesBased
{
    public FiniteStateMachineAttributesBased(ICoreAPI api, List<HashSet<string>> stateElements, string initialState, int id = 0) : base(GetStateManager(api, initialState, id, stateElements), GetStateResolver(stateElements))
    {
        _api = api;
        StateDimension = stateElements.Count;
    }

    public int StateDimension { get; }

    public bool Init(object owner, CollectibleObject collectible)
    {
        try
        {
            Dictionary<string, IInput> inputs = CollectInputs(owner);
            Dictionary<InputHandlerDelegate, InputHandlerAttribute> handlers = CollectHandlers(owner);

            RegisterInputs(inputs, collectible);

            foreach ((InputHandlerDelegate handler, InputHandlerAttribute attribute) in handlers)
            {
                AddHandler(handler, attribute, inputs);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(_api, this, $"Error while initializing FSM for class '{Logger.GetCallerTypeName(owner)}':\n{exception.Message}");
            return false;
        }

        return true;
    }

    public bool SetState(ItemSlot slot, int elementIndex, string value)
    {
        if (elementIndex >= StateDimension) throw new ArgumentException($"Tried to set state element with index '{elementIndex}' greater or equal to state dimension '{StateDimension}'.", nameof(elementIndex));
        IState current = StateManager.Get(slot);
        return SetState(slot, Substitute(current, elementIndex, value));
    }
    public bool SetState(ItemSlot slot, params (int elementIndex, string value)[] elements)
    {
        IState state = StateManager.Get(slot);
        foreach ((int elementIndex, string value) in elements)
        {
            if (elementIndex >= StateDimension) throw new ArgumentException($"Tried to set value '{value}' to state element with index '{elementIndex}' greater or equal to state dimension '{StateDimension}'.");

            state = Substitute(state, elementIndex, value);
        }
        return SetState(slot, state);
    }

    private readonly ICoreAPI _api;

    private static Dictionary<string, IInput> CollectInputs(object owner)
    {
        IEnumerable<PropertyInfo> properties = owner.GetType().GetProperties().Where(property => property.GetCustomAttributes(typeof(InputAttribute), true).Any());

        Dictionary<string, IInput> inputs = new();
        foreach (PropertyInfo property in properties)
        {
            if (property.GetCustomAttributes(typeof(InputAttribute), true)[0] is not InputAttribute attribute) continue;

            if (property.GetValue(owner) is not IInput input)
            {
                throw new InvalidOperationException($"Input with code '{attribute.Code}' specified via property should implement 'IInput' interface.");
            }

            inputs.Add(attribute.Code, input);
        }

        return inputs;
    }
    private static Dictionary<InputHandlerDelegate, InputHandlerAttribute> CollectHandlers(object owner)
    {
        IEnumerable<MethodInfo> methods = owner.GetType().GetMethods().Where(method => method.GetCustomAttributes(typeof(InputHandlerAttribute), true).Any());

        Dictionary<InputHandlerDelegate, InputHandlerAttribute> handlers = new();
        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttributes(typeof(InputHandlerAttribute), true)[0] is not InputHandlerAttribute attribute) continue;

            if (Delegate.CreateDelegate(typeof(InputHandlerDelegate), owner, method) is not InputHandlerDelegate handler)
            {
                throw new InvalidOperationException($"Handler for states '{Utils.PrintList(attribute.States)}' and input '{Utils.PrintList(attribute.Inputs)}' should have same signature as 'InputHandlerDelegate' delegate.");
            }

            handlers.Add(handler, attribute);
        }

        return handlers;
    }
    private void RegisterInputs(Dictionary<string, IInput> inputs, CollectibleObject collectible)
    {
        IInputManager? inputManager = _api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetInputManager();
        if (inputManager == null) return;

        foreach ((_, IInput input) in inputs)
        {
            inputManager.RegisterInput(input, Process, collectible);
        }
    }
    private void AddHandler(InputHandlerDelegate handler, InputHandlerAttribute attribute, Dictionary<string, IInput> inputs)
    {
        HashSet<IState> states = ResolveStatesForHandler(attribute);

        foreach (IState state in states)
        {
            if (!Handlers.ContainsKey(state)) Handlers.Add(state, new());

            foreach (string inputCode in attribute.Inputs)
            {
                if (!inputs.ContainsKey(inputCode))
                {
                    throw new InvalidFilterCriteriaException($"Input with code '{inputCode}' not found");
                }

                IInput input = inputs[inputCode];

                if (Handlers[state].ContainsKey(input))
                {
                    throw new InvalidFilterCriteriaException($"Halder for input with code '{inputCode}' and state '{state}' already exists. Each pair (input,state) can have only one handler.");
                }

                Handlers[state].Add(input, handler);
            }
        }
    }
    private HashSet<IState> ResolveStatesForHandler(InputHandlerAttribute attribute)
    {
        HashSet<IState> states = new();
        foreach (string stateWildcard in attribute.States)
        {
            IEnumerable<IState> resolvedStates = StateResolver.ResolveStates(stateWildcard).Select(StateManager.DeserializeState);
            foreach (IState state in resolvedStates)
            {
                states.Add(state);
            }
        }
        return states;
    }
    private IState Substitute(IState state, int element, string value)
    {
        return StateDimension switch
        {
            1 => StateManager.DeserializeState(value),
            2 => VectorState2.Substitute(state as VectorState2, element, value),
            3 => VectorState3.Substitute(state as VectorState3, element, value),
            4 => VectorState4.Substitute(state as VectorState4, element, value),
            _ => VectorState.Substitute(state as VectorState, element, value)
        };
    }

    private static IStateManager GetStateManager(ICoreAPI api, string initialState, int id, List<HashSet<string>> stateElements)
    {
        System.Func<string, IState> deserializer = stateElements.Count switch
        {
            1 => State.Deserialize,
            2 => VectorState2.Deserialize,
            3 => VectorState3.Deserialize,
            4 => VectorState4.Deserialize,
            _ => VectorState.Deserialize
        };

        StateManager manager = new(api, initialState, id, deserializer);
        return manager;
    }
    private static IStateResolver GetStateResolver(List<HashSet<string>> stateElements)
    {
        StateResolver resolver = new()
        {
            States = stateElements
        };
        resolver.Initialize();
        return resolver;
    }
}


[AttributeUsage(AttributeTargets.Method)]
public class InputHandlerAttribute : Attribute
{
    public string[] States { get; }
    public string[] Inputs { get; }

    public InputHandlerAttribute(string state, params string[] inputs)
    {
        States = new string[] { state };
        Inputs = inputs;
    }
    public InputHandlerAttribute(string[] states, params string[] inputs)
    {
        States = states;
        Inputs = inputs;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class InputAttribute : Attribute
{
    public string Code { get; }

    public InputAttribute(string code)
    {
        Code = code;
    }
}

public class TestClass
{
    [Input("RMB")]
    public MouseKey Interact { get; set; }
    [Input("LMB")]
    public MouseKey Attack { get; set; }
    [Input("SlotSelected")]
    public BeforeSlotChanged Selected { get; set; }


    public TestClass(IFiniteStateMachineAttributesBased fsm, CollectibleObject collectible)
    {
        _fsm = fsm;
        _fsm.Init(this, collectible);
    }

    [InputHandler(state: "test-state-*", "RMB", "LMB")]
    public bool Handler1(ItemSlot slot, IPlayer? player, IInput input, IState state)
    {
        return true;
    }

    [InputHandler(state: "*", "SlotSelected")]
    public bool Handler2(ItemSlot slot, IPlayer? player, IInput input, IState state)
    {
        return true;
    }

    private IFiniteStateMachineAttributesBased _fsm;
}