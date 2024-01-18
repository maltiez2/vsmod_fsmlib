using SimpleExpressionEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace MaltiezFSM.Framework;

public sealed class AttributeReferencesManager : IDisposable
{
    private const string cCollectiblePostfix = "FromAttr";
    private const string cItemStackPostfix = "FromStackAttr";
    private bool mDisposed = false;
    private readonly Dictionary<string, JsonObject> mCache = new();

    public void Substitude(JsonObject where, CollectibleObject from)
    {
        
    }

    public void Substitude(JsonObject where, ItemSlot from)
    {
        
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;

        


    }
}

internal sealed class AttributeModifier
{
    private readonly Node mFormula;
    private readonly ICoreAPI mApi;
    private readonly AttributeGetter mGetter;

    public AttributeModifier(ICoreAPI api, string formula, AttributeGetter getter)
    {
        mFormula = Parser.Parse(formula);
        mApi = api;
        mGetter = getter;
    }

    public float Calc(IPlayer player, ItemStack stack) => (float)mFormula.Eval(new ItemStackAttributeContext(mApi, new(mApi, player, 0), mGetter, stack));
    public float Calc(IPlayer player, CollectibleObject collectible) => (float)mFormula.Eval(new CollectibleAttributeContext(mApi, new(mApi, player, 0), mGetter, collectible));
}

internal sealed class AttributeGetter
{
    private readonly Dictionary<string, ItemStackAttributePath> mStackAttributes = new();
    private readonly Dictionary<string, CollectibleAttributePath> mCollectibleAttributes = new();
    private readonly Dictionary<string, string> mAlreadyRegistered = new();

    public void Add(string name, string path)
    {
        if (mAlreadyRegistered.ContainsKey(path))
        {
            string registered = mAlreadyRegistered[path];
            mStackAttributes.TryAdd(name, mStackAttributes[registered]);
            mCollectibleAttributes.TryAdd(name, mCollectibleAttributes[registered]);
        }
        else
        {
            mStackAttributes.TryAdd(name, new(path));
            mCollectibleAttributes.TryAdd(name, new(path));
            mAlreadyRegistered.Add(path, name);
        }
    }

    public double? GetDouble(string name, ITreeAttribute? attributes)
    {
        return GetDouble(mStackAttributes[name].Get(attributes));
    }

    public double? GetDouble(string name, JsonObject? attributes)
    {
        return mCollectibleAttributes[name].Get(attributes)?.AsDouble();
    }

    public IAttribute? Get(string name, ITreeAttribute? attributes)
    {
        return mStackAttributes[name].Get(attributes);
    }
    public JsonObject? Get(string name, JsonObject? attributes)
    {
        return mCollectibleAttributes[name].Get(attributes);
    }

    private double? GetDouble(IAttribute? attribute)
    {
        return
            (attribute as DoubleAttribute)?.value ??
            (attribute as FloatAttribute)?.value ??
            (attribute as IntAttribute)?.value;
    }
}

internal sealed class ItemStackAttributePath
{
    private delegate IAttribute? PathElement(IAttribute? attribute);

    private IAttribute? PathElementByIndex(IAttribute? attribute, int index) => (attribute as TreeArrayAttribute)?.value?[index];
    private IAttribute? PathElementByKey(IAttribute? attribute, string key) => (attribute as ITreeAttribute)?[key];

    private readonly IEnumerable<PathElement> mPath;

    public ItemStackAttributePath(string path)
    {
        mPath = path.Split("/").Where(element => element != "").Select(Convert);
    }

    private PathElement Convert(string element)
    {
        if (int.TryParse(element, out int index))
        {
            return tree => PathElementByIndex(tree, index);
        }
        else
        {
            return tree => PathElementByKey(tree, element);
        }
    }

    public IAttribute? Get(IAttribute? tree)
    {
        IAttribute? result = tree;
        foreach (var element in mPath)
        {
            result = element.Invoke(result);
            if (result == null) return null;
        }
        return result;
    }
}

internal sealed class CollectibleAttributePath
{
    private delegate JsonObject? PathElement(JsonObject? attribute);

    private JsonObject? PathElementByIndex(JsonObject? attribute, int index) => attribute?.IsArray() == true ? attribute.AsArray()[index] : null;
    private JsonObject? PathElementByKey(JsonObject? attribute, string key) => attribute?.KeyExists(key) == true ? attribute[key] : null;

    private readonly IEnumerable<PathElement> mPath;

    public CollectibleAttributePath(string path)
    {
        mPath = path.Split("/").Where(element => element != "").Select(Convert);
    }

    private PathElement Convert(string element)
    {
        if (int.TryParse(element, out int index))
        {
            return tree => PathElementByIndex(tree, index);
        }
        else
        {
            return tree => PathElementByKey(tree, element);
        }
    }

    public JsonObject? Get(JsonObject? tree)
    {
        JsonObject? result = tree;
        foreach (var element in mPath)
        {
            result = element.Invoke(result);
            if (result == null) return null;
        }
        return result;
    }
}


internal sealed class ItemStackAttributeContext : BaseContext
{
    private readonly ItemStack? mStack;
    private readonly AttributeGetter mGetter;
    private readonly StatsContext mStats;

    public ItemStackAttributeContext(ICoreAPI api, StatsContext stats, AttributeGetter getter, ItemStack? stack) : base(api)
    {
        mStack = stack;
        mGetter = getter;
        mStats = stats;
    }

    public override double ResolveVariable(string name)
    {
        return mGetter.GetDouble(name, mStack?.Attributes) ?? mStats.ResolveVariable(name);
    }
}

internal sealed class CollectibleAttributeContext : BaseContext
{
    private readonly CollectibleObject? mCollectible;
    private readonly AttributeGetter mGetter;
    private readonly StatsContext mStats;

    public CollectibleAttributeContext(ICoreAPI api, StatsContext stats, AttributeGetter getter, CollectibleObject? collectible) : base(api)
    {
        mCollectible = collectible;
        mGetter = getter;
        mStats = stats;
    }

    public override double ResolveVariable(string name)
    {
        return mGetter.GetDouble(name, mCollectible?.Attributes) ?? mStats.ResolveVariable(name);
    }
}