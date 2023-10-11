﻿using HarmonyLib;
using MaltiezFSM.API;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Systems
{
    internal class BasicPlayerStats : UniqueIdFactoryObject, ISystem
    {
        public const int cDefaultTimeout = 30 * 1000;
        public const float cDefaultValue = 0;
        
        private ICoreAPI mApi;
        private string mStatCode;
        private Dictionary<string, TimeoutCallback> mStats = new();

        public override void Init(string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api)
        {
            mApi = api;
            mStatCode = "FSMLib." + code;
        }

        void ISystem.SetSystems(Dictionary<string, ISystem> systems)
        {
        }

        bool ISystem.Process(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            string code = parameters["code"].AsString();
            float value = parameters["value"].AsFloat(0);
            bool persistent = parameters["persist"].AsBool(false);
            int timeout = parameters["timeout"].AsInt(0);
            float defaultValue = cDefaultValue;
            if (parameters.KeyExists("default"))
            {
                defaultValue = parameters["default"].AsFloat(0);
            }

            if (mStats.ContainsKey(code))
            {
                mStats[code].Stop();
            }

            (player as EntityPlayer).Stats.Set(code, mStatCode, value, persistent);
            mStats.TryAdd(code, new());
            mStats[code].Start(mApi, timeout > 0 ? timeout : cDefaultTimeout, (float time) => Revert(time, player, code, defaultValue));

            return true;
        }

        bool ISystem.Verify(ItemSlot slot, EntityAgent player, JsonObject parameters)
        {
            return true;
        }
        void Revert(float time, EntityAgent player, string category, float defaultValue)
        {
            mApi.Logger.Debug("[FSMlib] [BasicPlayerStats] Timeout triggered after " + time);
            (player as EntityPlayer)?.Stats.Set(category, mStatCode, defaultValue, true);
            if (mStats.ContainsKey(category)) mStats.Remove(category);
        }
    }

    public sealed class TimeoutCallback
    {
        private ICoreAPI mApi;
        private long mCallbackId;

        public void Start(ICoreAPI api, int delay_ms, Action<float> callback)
        {
            mApi = api;
            mCallbackId = mApi.World.RegisterCallback(callback, delay_ms);
        }
        public void Stop()
        {
            mApi.World.UnregisterCallback(mCallbackId);
        }
    }
}
