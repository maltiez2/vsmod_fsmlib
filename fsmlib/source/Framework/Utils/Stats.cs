using SimpleExpressionEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Security.Cryptography;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public class StatsModifier
{
    private readonly INode<float, float, float> mFormula;
    private readonly ICoreAPI mApi;

    public StatsModifier(ICoreAPI api, string formula)
    {
        mFormula = MathParser.Parse(formula);
        mApi = api;
    }

    public float Calc(IPlayer player, float value)
    {
        return mFormula.Evaluate(new CombinedContext<float, float>(new List<IContext<float, float>>() { new ValueContext(value), new MathContext(), new StatsContext<float>(player) }));
    }
    public TimeSpan CalcMilliseconds(IPlayer player, TimeSpan value)
    {
        return TimeSpan.FromMilliseconds(mFormula.Evaluate(new CombinedContext<float, float>(new List<IContext<float, float>>() { new ValueContext((float)value.TotalMilliseconds), new MathContext(), new StatsContext<float>(player) })));
    }
    public TimeSpan CalcSeconds(IPlayer player, TimeSpan value)
    {
        return TimeSpan.FromSeconds(mFormula.Evaluate(new CombinedContext<float, float>(new List<IContext<float, float>>() { new ValueContext((float)value.TotalSeconds), new MathContext(), new StatsContext<float>(player) })));
    }
}

public sealed class ValueContext : IContext<float, float>
{
    private readonly float mValue;

    public ValueContext(float value)
    {
        mValue = value;
    }

    public bool Resolvable(string name)
    {
        return name switch
        {
            "value" => true,
            _ => false
        };
    }

    public float Resolve(string name, params float[] arguments)
    {
        return name switch
        {
            "value" => mValue,
            _ => 0
        };
    }
}