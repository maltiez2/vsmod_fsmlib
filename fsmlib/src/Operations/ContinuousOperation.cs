using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Operations
{
    public class Continuous : Branched
    {
        public class SystemRequest
        {
            public string Code { get; set; }
            public JsonObject Attributes { get; set; }
            public ISystem System { get; set; }

            public bool Process(ItemSlot slot, EntityAgent player)
            {
                if (System.Verify(slot, player, Attributes))
                {
                    return System.Process(slot, player, Attributes);
                }

                return false;
            }
        }

        protected readonly Dictionary<int, List<SystemRequest>> mTimedSystems = new();
        protected readonly Dictionary<long, List<long>> mSystemsTimers = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            base.Init(code, definition, collectible, api);

            foreach (JsonObject system in definition[systemsAttrName]["timed"].AsArray())
            {
                string systemCode = system["code"].AsString();
                int time = system["time"].AsInt();

                if (!mTimedSystems.ContainsKey(time)) mTimedSystems.Add(time, new List<SystemRequest>());

                mTimedSystems[time].Add(new() { Code = systemCode, Attributes = system });
            }
        }

        public override void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems)
        {
            base.SetInputsStatesSystems(inputs, states, systems);

            foreach ((_, List<SystemRequest> requests) in mTimedSystems)
            {
                foreach (SystemRequest request in requests)
                {
                    request.System = systems[request.Code];
                }
            }
        }

        public override bool StopTimer(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            bool stop = base.StopTimer(slot, player, state, input);

            if (stop) StopTimedSystems(player.EntityId);

            return stop;
        }

        public override IState Perform(ItemSlot slot, EntityAgent player, IState state, IInput input)
        {
            IState nextState = base.Perform(slot, player, state, input);
            if (state == nextState) return state;

            StartTimedSystems(slot, player);

            return nextState;
        }

        protected void StartTimedSystems(ItemSlot slot, EntityAgent player)
        {
            long entityId = player.EntityId;

            if (!mSystemsTimers.ContainsKey(entityId))
            {
                mSystemsTimers.Add(entityId, new());
            }
            else
            {
                StopTimedSystems(entityId);
            }

            foreach ((int time, _) in mTimedSystems)
            {
                long id = mApi.World.RegisterGameTickListener(_ => RunSystem(time, slot, player), 0);
                mSystemsTimers[entityId].Add(id);
            }
        }

        protected void StopTimedSystems(long entityId)
        {
            foreach (long timer in mSystemsTimers[entityId])
            {
                mApi.World.UnregisterCallback(timer);
            }
            mSystemsTimers[entityId].Clear();
        }

        protected void RunSystem(int time, ItemSlot slot, EntityAgent player)
        {
            foreach (SystemRequest request in mTimedSystems[time])
            {
                request.Process(slot, player);
            }
        }
    }
}
