using MaltiezFSM.API;
using MaltiezFSM.Framework;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using VSImGui;
using static MaltiezFSM.API.IOperation;

namespace MaltiezFSM.Operations;

public abstract class BaseOperation : FactoryProduct, IOperation
{
    protected readonly Dictionary<ISystem, string> mSystemsCodes = new();

    private bool mDisposed = false;
    private readonly IAttributeReferencesManager? mRequestProcessor;

    protected BaseOperation(int id, string code, JsonObject definition, CollectibleObject collectible, ICoreAPI api) : base(id, code, definition, collectible, api)
    {
        mRequestProcessor = api.ModLoader.GetModSystem<FiniteStateMachineSystem>().GetAttributeReferencesManager();
    }

    public abstract List<Transition> GetTransitions();
    public abstract void SetInputsStatesSystems(Dictionary<string, IInput> inputs, Dictionary<string, IState> states, Dictionary<string, ISystem> systems);
    public abstract Outcome Verify(ItemSlot slot, IPlayer player, IState state, IInput input);
    public abstract Result Perform(ItemSlot slot, IPlayer player, IState state, IInput input);

    private readonly Dictionary<int, JsonObject> mRequestsCache = new();
    protected bool Verify(ItemSlot slot, IPlayer player, IEnumerable<(ISystem system, JsonObject request)> systems)
    {
        foreach ((ISystem system, JsonObject request) in systems)
        {
            int hash = request.GetHashCode();
            mRequestsCache[hash] = request.Clone();

            try
            {
                mRequestProcessor?.Substitute(mRequestsCache[hash], slot);
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"System '{mSystemsCodes[system]}' crashed while substituting 'FromAttr' values in '{mCode}' operation in '{mCollectible.Code}' collectible");
                Logger.Verbose(mApi, this, $"System '{mSystemsCodes[system]}' crashed while substituting 'FromAttr' values in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{request}\n\nException:\n{exception}\n");
                continue;
            }

            try
            {
                bool result = system.Verify(slot, player, mRequestsCache[hash]);

                SystemsDebugWindow.Enqueue(system, this, player, request, result, mApi.Side == EnumAppSide.Client);
                
                if (!result)
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"System '{mSystemsCodes[system]}' crashed while verification in '{mCode}' operation in '{mCollectible.Code}' collectible");
                Logger.Verbose(mApi, this, $"System '{mSystemsCodes[system]}' crashed while verification in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{mRequestsCache[hash]}\n\nException:\n{exception}\n");
            }
        }

        return true;
    }
    protected void Process(ItemSlot slot, IPlayer player, IEnumerable<(ISystem system, JsonObject request)> systems)
    {
        foreach ((ISystem system, JsonObject request) in systems)
        {
            int hash = request.GetHashCode();

            try
            {
                system.Process(slot, player, mRequestsCache[hash]);
            }
            catch (Exception exception)
            {
                Logger.Error(mApi, this, $"System '{mSystemsCodes[system]}' crashed while processing in '{mCode}' operation in '{mCollectible.Code}' collectible");
                Logger.Verbose(mApi, this, $"System '{mSystemsCodes[system]}' crashed while processing in '{mCode}' operation in '{mCollectible.Code}' collectible.\n\nRequest:{mRequestsCache[hash]}\n\nException:\n{exception}\n");
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!mDisposed)
        {
            if (disposing)
            {
                // Nothing to dispose
            }

            mDisposed = true;
        }
    }
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
