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
    /// <summary>
    /// Represents transition between main FSM states. Constructs part of a FSM state graph and interacts with <see cref="ISystem">systems</see>.
    /// </summary>
    public interface IOperation : IFactoryObject
    {
        /// <summary>
        /// Is called when particular <see cref="IInput"/> is caught and FSM is in an appropriate state.
        /// </summary>
        /// <param name="slot">Slot containing <see cref="CollectibleObject"/> specified in <see cref="IFactoryObject.Init"/></param>
        /// <param name="player">Player that owns provided slot</param>
        /// <param name="state">Current FSM state</param>
        /// <param name="input">Input that triggered this operation</param>
        /// <returns>State that FSM will switch to</returns>
        IState Perform(ItemSlot slot, EntityAgent player, IState state, IInput input);

        bool StopTimer(ItemSlot slot, EntityAgent player, IState state, IInput input);

        /// <summary>
        /// Called by FSM after successful <see cref="Perform"/> call. Used to set up timer that will call the same operation with the same parameters except for state, that will be equal to new FSM state
        /// </summary>
        /// <param name="slot">Slot containing <see cref="CollectibleObject"/> specified in <see cref="IFactoryObject.Init"/>, same as previously provided to <see cref="Perform"/></param>
        /// <param name="player">Player that owns provided slot, same as previously provided to <see cref="Perform"/></param>
        /// <param name="state">FSM state, same as previously provided to <see cref="Perform"/></param>
        /// <param name="input">Input that triggered this operation, same as previously provided to <see cref="Perform"/></param>
        /// <returns>Time in <b>milliseconds</b>, timer will not be created if value is equal to <c>null</c></returns>
        int? Timer(ItemSlot slot, EntityAgent player, IState state, IInput input);

        struct Transition
        {
            public string input { get; set; }
            public string fromState { get; set; }
            public string toState { get; set; }

            public static implicit operator Transition((string input, string fromState, string toState) parameters)
            {
                return new Transition() { input = parameters.input, fromState = parameters.fromState, toState = parameters.toState };
            }
        }
        List<Transition> GetTransitions();

        /// <summary>
        /// Called by FSM after calling <see cref="GetInitialStates"/> and <see cref="GetFinalStates"/> and <see cref="GetInputs"/>
        /// </summary>
        /// <param name="inputs">Map to <see cref="IInput"/> objects from inputs codes given to FSM in <see cref="GetInputs"/></param>
        /// <param name="states">Map to <see cref="IState"/> objects from states codes given to FSM in <see cref="GetInitialStates"/> and <see cref="GetFinalStates"/></param>
        /// <param name="systems">Map from states codes to all available <see cref="ISystem"/> objects</param>
        void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems);
    }
    public interface ISystem : IFactoryObject
    {
        void SetSystems(Dictionary<string, ISystem> systems);
        bool Verify(ItemSlot slot, EntityAgent player, JsonObject parameters);
        bool Process(ItemSlot slot, EntityAgent player, JsonObject parameters);
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
    public interface IFiniteStateMachine
    {
        void Init(ICoreAPI api, Dictionary<string, IOperation> operations, Dictionary<string, ISystem> systems, Dictionary<string, IInput> inputs, JsonObject behaviourAttributes, CollectibleObject collectible, IOperationInputInvoker? invoker);
        bool Process(ItemSlot slot, EntityAgent player, IInput input);
        bool OnTimer(ItemSlot slot, EntityAgent player, IInput input, IOperation operation);
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