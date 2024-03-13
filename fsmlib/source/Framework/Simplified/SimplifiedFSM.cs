using MaltiezFSM.API;
using MaltiezFSM.Inputs;
using System;
using System.Reflection;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public delegate bool InputHandlerDelegate(ItemSlot slot, IPlayer? player, IInput input, IState state);

public interface IFiniteStateMachine
{
    Dictionary<IState, Dictionary<IInput, InputHandlerDelegate>> Handlers { get; set; }

    event Action<ItemSlot, IState>? StateChanged;

    bool SetState(ItemSlot slot, IState state);
    IState GetState(ItemSlot slot);
    IState DeserializeState(string state);
    IEnumerable<IState> ResolverState(string wildcard);
}
public interface IFiniteStateMachineWithInit
{
    bool Init(object owner, CollectibleObject collectible);
}

public class FiniteStateMachineSimplified : IFiniteStateMachine
{
    public Dictionary<IState, Dictionary<IInput, InputHandlerDelegate>> Handlers { get; set; } = new();
    public bool ResetStateOnInvalid { get; set; } = true;

    public event Action<ItemSlot, IState>? StateChanged;

    public FiniteStateMachineSimplified(
        IStateManager stateManager,
        IStateResolver stateResolver
    )
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

public class FiniteStateMachineWithInit : FiniteStateMachineSimplified, IFiniteStateMachineWithInit
{
    public FiniteStateMachineWithInit(ICoreAPI api, List<HashSet<string>> stateElements, string initialState) : base(GetStateManager(api, initialState), GetStateResolver(stateElements))
    {
        _api = api;
    }

    public bool Init(object owner, CollectibleObject collectible)
    {
        Dictionary<string, IInput> inputs = CollectInputs(owner);
        Dictionary<InputHandlerDelegate, InputHandlerAttribute> handlers = CollectHandlers(owner);

        RegisterInputs(inputs, collectible);

        foreach ((InputHandlerDelegate handler, InputHandlerAttribute attribute) in handlers)
        {
            AddHandler(handler, attribute, inputs);
        }

        return true;
    }

    private ICoreAPI _api;
    private Dictionary<string, IInput> CollectInputs(object owner)
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
    private Dictionary<InputHandlerDelegate, InputHandlerAttribute> CollectHandlers(object owner)
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


    private static int _nextId = 0;
    private static IStateManager GetStateManager(ICoreAPI api, string initialState)
    {
        StateManager manager = new(api, initialState, _nextId++);
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

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
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


    public TestClass(IFiniteStateMachineWithInit fsm, CollectibleObject collectible)
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

    private IFiniteStateMachineWithInit _fsm;
}