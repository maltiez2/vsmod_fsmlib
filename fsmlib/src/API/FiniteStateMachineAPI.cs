using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.API
{
    public struct KeyPressModifiers
    {
        public bool? Alt { get; set; }
        public bool? Ctrl { get; set; }
        public bool? Shift { get; set; }

        public KeyPressModifiers(bool? alt, bool? ctrl, bool? shift)
        {
            Alt = alt;
            Ctrl = ctrl;
            Shift = shift;
        }

        public bool AltAsBool(bool defaultValue = false) => Alt == null ? defaultValue : (bool)Alt;
        public bool CtrlAsBool(bool defaultValue = false) => Ctrl == null ? defaultValue : (bool)Ctrl;
        public bool ShiftAsBool(bool defaultValue = false) => Shift == null ? defaultValue : (bool)Shift;
        public string[] GetCodes()
        {
            List<string> codes = new();
            if (Alt == true) codes.Add("alt");
            if (Ctrl == true) codes.Add("ctrl");
            if (Shift == true) codes.Add("shift");
            return codes.ToArray();
        }
    }

    // DOCUMENTATION: WORK IN PROGRESS

    /// <summary>
    /// Is used to represent a state of FSM. Is used as dictionary key and to compare if two states are the same state.
    /// </summary>
    public interface IState
    {
    }

    /// <summary>
    /// Used by <see cref="IFactory{ProductClass}"/> to produce instances of given class.<br/>
    /// Immediately after instantiating object <see cref="Init"/> is called, and then <see cref="SetId"/>.<br/>
    /// <c>id</c> given by <see cref="IFactory{ProductClass}"/> is meant to be unique among all instances produced all factories.
    /// </summary>
    public interface IFactoryObject
    {
        /// <summary>
        /// Called immediately after instantiating the object
        /// </summary>
        /// <param name="code">Given to the factory by factory user, usually unique per collectible</param>
        /// <param name="definition">Given to the factory by factory user, usually consists from attributes defined in <c>itemtype</c></param>
        /// <param name="collectible">This factory object will be used only by given collectible</param>
        void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api);

        /// <summary>
        /// Called immediately after <see cref="Init"/>>
        /// </summary>
        /// <param name="id">Given by <see cref="IFactory{TProductClass}"/> and is meant to be unique among all instances produced by all factories </param>
        void SetId(int id);

        /// <returns>Unique id given to the object by calling <see cref="SetId"/></returns>
        int GetId();
    }

    public interface IOperation : IFactoryObject, IDisposable
    {
        public enum Outcome
        {
            None,
            Started,
            Failed,
            Finished,
            StartedAndFinished
        }
        public enum Timeout
        {
            Ignore,
            Start,
            Stop
        }
        public readonly struct Result
        {
            public IState State { get; }
            public Outcome Outcome { get; }
            public Timeout Timeout { get; }
            public TimeSpan TimeoutDelay { get; }

            public Result(IState state, Outcome outcome, Timeout timeout, TimeSpan? delay = null)
            {
                State = state;
                Outcome = outcome;
                Timeout = timeout;
                TimeoutDelay = delay ?? new TimeSpan(0);
            }
        }

        Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input);

        Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input);

        struct Transition
        {
            public enum TriggerType
            {
                Input,
                Timeout
            }
            
            public TriggerType Trigger { get; set; }
            public string? Input { get; set; }
            public string FromState { get; set; }
            public string ToState { get; set; }

            public static Transition InputTrigger(string input, string fromState, string toState)
            {
                return new Transition() { Trigger = TriggerType.Input, Input = input, FromState = fromState, ToState = toState };
            }

            public static Transition TimeoutTrigger(string fromState, string toState)
            {
                return new Transition() { Trigger = TriggerType.Timeout, Input = null, FromState = fromState, ToState = toState };
            }

            public static implicit operator Transition((string input, string fromState, string toState) parameters)
            {
                return new Transition() { Trigger = TriggerType.Input, Input = parameters.input, FromState = parameters.fromState, ToState = parameters.toState };
            }
        }
        List<Transition> GetTransitions();

        void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems);
    }
    public interface ISystem : IFactoryObject
    {
        void SetSystems(Dictionary<string, ISystem> systems);
        bool Verify(ItemSlot slot, IPlayer player, JsonObject parameters);
        bool Process(ItemSlot slot, IPlayer player, JsonObject parameters);
        string[] GetDescription(ItemSlot slot, IWorldAccessor world);
    }

    public interface IFactory<TProductClass>
    {
        Type TypeOf(string name);
        void Register<TObjectClass>(string name) where TObjectClass : TProductClass, new();
        void SubstituteWith<TObjectClass>(string name) where TObjectClass : TProductClass, new();
        TProductClass Instantiate(string code, string name, JsonObject definition, CollectibleObject collectible);
    }
    public interface IBehaviourAttributesParser
    {
        bool ParseDefinition(IFactory<IOperation> operationTypes, IFactory<ISystem> systemTypes, IFactory<IInput> inputTypes, JsonObject behaviourAttributes, CollectibleObject collectible);
        Dictionary<string, IOperation> GetOperations();
        Dictionary<string, ISystem> GetSystems();
        Dictionary<string, IInput> GetInputs();
    }
    public interface IFiniteStateMachine : IDisposable
    {
        bool Process(ItemSlot slot, IPlayer player, IInput input);
        bool OnTimer(ItemSlot slot, IPlayer player, IInput input, IOperation operation);
        List<IInput> GetAvailableInputs(ItemSlot slot);
    }
    public interface IOperationInputInvoker
    {
        bool Started(IOperation operation, ItemSlot inSlot, IPlayer player);
        bool Finished(IOperation operation, ItemSlot inSlot, IPlayer player);
    }

    public interface IFactoryProvider
    {
        IFactory<IOperation> GetOperationFactory();
        IFactory<ISystem> GetSystemFactory();
        IFactory<IInput> GetInputFactory();
    }
    public interface IUniqueIdGeneratorForFactory
    {
        int GenerateInstanceId();
        int GetFactoryId();
    }
    public interface IActiveSlotListener
    {
        enum SlotEventType
        {
            ItemDropped
        }

        int RegisterListener(SlotEventType eventType, System.Func<int, bool> callback); // handled callback(item slot id)
        void UnregisterListener(int id);
    }

    public interface ITransformManager
    {
        void SetTransform(long entityId, string transformType, EnumItemRenderTarget target, ModelTransform transform);
        void ResetTransform(long entityId, string transformType, EnumItemRenderTarget target);
        ModelTransform GetTransform(long entityId, string transformType, EnumItemRenderTarget target);
        ModelTransform CalcCurrentTransform(long entityId, EnumItemRenderTarget target);
        void SetEntityId(long entityId, ItemStack item);
        long? GetEntityId(ItemStack item);
    }

    public interface ITransformManagerProvider
    {
        ITransformManager GetTransformManager();
    }
}