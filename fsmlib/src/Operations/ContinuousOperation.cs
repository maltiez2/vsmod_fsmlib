using MaltiezFSM.API;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Operations
{
    public class Continuous : Delayed
    {
        public class SystemRequest
        {
            public string Code { get; set; }
            public JsonObject Attributes { get; set; }
            public ISystem System { get; set; }

            public bool Process(ItemSlot slot, IPlayer player)
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

            foreach (JsonObject system in definition["systems"]["timed"].AsArray())
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

        public override IOperation.Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input)
        {
            var result = base.Perform(slot, player, state, input);

            if (result.Outcome == IOperation.Outcome.Finished)
            {
                StopTimedSystems(player.Entity.EntityId);
            }

            if (result.Outcome == IOperation.Outcome.Started)
            {
                StartTimedSystems(slot, player);
            }

            return result;
        }

        protected void StartTimedSystems(ItemSlot slot, IPlayer player)
        {
            long entityId = player.Entity.EntityId;

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

        protected void RunSystem(int time, ItemSlot slot, IPlayer player)
        {
            foreach (SystemRequest request in mTimedSystems[time])
            {
                request.Process(slot, player);
            }
        }
    }
}
