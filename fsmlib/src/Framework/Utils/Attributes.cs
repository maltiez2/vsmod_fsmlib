using MaltiezFSM.API;
using Newtonsoft.Json.Linq;
using SimpleExpressionEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using VSImGui;

namespace MaltiezFSM.Framework;

public sealed class AttributeReferencesManager : IAttributeReferencesManager
{
    private const string cPostfix = "FromAttr";
    private bool mDisposed = false;
    private readonly Dictionary<int, AttributeModifier> mCache = new();
    private readonly AttributeGetter mAttributes = new();
    private readonly ICoreAPI mApi;

    public AttributeReferencesManager(ICoreAPI api)
    {
        mApi = api;
    }

    public void Substitute<TFrom>(JsonObject where, TFrom from)
    {
        SubstituteRecursive(where.Token, from);
    }

    private void SubstituteRecursive<TFrom>(JToken where, TFrom from)
    {
        if (where is JArray whereArray)
        {
            foreach (JToken item in whereArray)
            {
                SubstituteRecursive(item, from);
            }
        }
        else if (where is JObject whereObject)
        {
            List<string> keys = new();
            foreach ((string key, JToken? item) in whereObject)
            {
                if (item is not JObject token) continue;
                if (!Match(key))
                {
                    SubstituteRecursive(item, from);
                    continue;
                }

                SubstituteToken(token, from);
                keys.Add(key);

            }

            foreach (string key in keys)
            {
                ReplaceToken(whereObject, key);
            }
        }
    }

    private void SubstituteToken<TFrom>(JObject where, TFrom from)
    {
        if (from is CollectibleObject collectible) SubstituteFromCollectible(where, collectible);
        if (from is ItemSlot slot) SubstituteFromStack(where, slot);
    }

    private static void ReplaceToken(JObject where, string key)
    {
        string newKey = key.Substring(0, key.Length - cPostfix.Length);

        where.Add(newKey, where[key]);
        where.Remove(key);
    }

    private void SubstituteFromCollectible(JObject where, CollectibleObject from)
    {
        float result = GetModifier(where).Calc(from);

        Replace(where, result);
    }

    private void SubstituteFromStack(JObject where, ItemSlot from)
    {
        IPlayer? player = (from.Inventory as InventoryBasePlayer)?.Player;
        ItemStack? stack = from.Itemstack;

        if (player == null || stack == null) return;

        float result = GetModifier(where).Calc(player, stack);

        Replace(where, result);
    }

    private static void Replace(JObject where, float with)
    {
        switch (where.Value<string>("type"))
        {
            case "int":
                where.Replace(new JValue((int)with));
                break;
            case "bool":
                where.Replace(new JValue((int)with != 0));
                break;
            default:
                where.Replace(new JValue(with));
                break;
        }
    }

    private AttributeModifier GetModifier(JObject where)
    {
        int hash = where.ToString().GetHashCode();

        if (mCache.ContainsKey(hash)) return mCache[hash];

        string formula = where.Value<string>("formula") ?? "0";

        foreach ((string key, JToken? item) in where)
        {
            if (key == "type") continue;

            if (item is not JValue itemValue || itemValue.Type != JTokenType.String) continue;

            string? path = (string?)itemValue.Value;

            if (path == null) continue;

            mAttributes.Add($"{key}_{hash}", path);
        }

        mCache.Add(hash, new(mApi, formula, mAttributes, hash));

        return mCache[hash];
    }

    private static bool Match(string key) => key.EndsWith(cPostfix);

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
    }
}

internal sealed class AttributeModifier
{
    private readonly INode<float, float, float> mFormula;
    private readonly ICoreAPI mApi;
    private readonly AttributeGetter mGetter;
    private readonly int mId;

    public AttributeModifier(ICoreAPI api, string formula, AttributeGetter getter, int id)
    {
        mFormula = MathParser.Parse(formula);
        mApi = api;
        mGetter = getter;
        mId = id;
    }

    public float Calc(IPlayer player, ItemStack stack) => mFormula.Evaluate(new CombinedContext<float, float>(new List<IContext<float, float>>() { new MathContext(), new ItemStackAttributeContext(mId, mApi, mGetter, stack), new StatsContext<float>(player) }));
    public float Calc(CollectibleObject collectible) => mFormula.Evaluate(new CombinedContext<float, float>(new List<IContext<float, float>>() { new MathContext(), new CollectibleAttributeContext(mId, mApi, mGetter, collectible) }));
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
        foreach (PathElement element in mPath)
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
        foreach (PathElement element in mPath)
        {
            result = element.Invoke(result);
            if (result == null) return null;
        }
        return result;
    }
}


internal sealed class ItemStackAttributeContext : IContext<float, float>
{
    private readonly ItemStack? mStack;
    private readonly AttributeGetter mGetter;
    private readonly int mId;

    public ItemStackAttributeContext(int id, ICoreAPI api, AttributeGetter getter, ItemStack? stack)
    {
        mStack = stack;
        mGetter = getter;
        mId = id;
    }

    public bool Resolvable(string name) => true;
    public float Resolve(string name, params float[] arguments) => (float)(mGetter.GetDouble($"{name}_{mId}", mStack?.Attributes) ?? mGetter.GetDouble($"{name}_{mId}", mStack?.Collectible?.Attributes) ?? 0);
}

internal sealed class CollectibleAttributeContext : IContext<float, float>
{
    private readonly CollectibleObject? mCollectible;
    private readonly AttributeGetter mGetter;
    private readonly int mId;
    private readonly ICoreAPI mApi;

    public CollectibleAttributeContext(int id, ICoreAPI api, AttributeGetter getter, CollectibleObject? collectible)
    {
        mCollectible = collectible;
        mGetter = getter;
        mApi = api;
        mId = id;
    }

    public bool Resolvable(string name) => true;
    public float Resolve(string name, params float[] arguments) => (float?)mGetter.GetDouble($"{name}_{mId}", mCollectible?.Attributes) ?? 0;
}