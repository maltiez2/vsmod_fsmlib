using SimpleExpressionEngine;
using System;
using Vintagestory.API.Common;

#nullable enable

namespace MaltiezFSM.Framework;

public class StatsModifier
{
    private readonly Node mFormula;
    private readonly ICoreAPI mApi;

    public StatsModifier(ICoreAPI api, string formula)
    {
        mFormula = Parser.Parse(formula);
        mApi = api;
    }

    public float Calc(IPlayer player, float value)
    {
        return (float)mFormula.Eval(new StatsContext(mApi, player, value));
    }
    public TimeSpan CalcMilliseconds(IPlayer player, TimeSpan value)
    {
        return TimeSpan.FromMilliseconds(mFormula.Eval(new StatsContext(mApi, player, (float)value.TotalMilliseconds)));
    }
    public TimeSpan CalcSeconds(IPlayer player, TimeSpan value)
    {
        return TimeSpan.FromSeconds(mFormula.Eval(new StatsContext(mApi, player, (float)value.TotalSeconds)));
    }
}

public class StatsContext : IContext
{
    private readonly ICoreAPI mApi;
    private readonly IPlayer mPlayer;
    private readonly float mValue;

    public StatsContext(ICoreAPI api, IPlayer player, float value)
    {
        mPlayer = player;
        mApi = api;
        mValue = value;
    }

    public double ResolveVariable(string name)
    {
        return name switch
        {
            "value" => mValue,
            "PI" => Math.PI,
            "E" => Math.E,
            _ => mPlayer.Entity.Stats.GetBlended(name)
        };
    }

    public double CallFunction(string name, double[] arguments)
    {
        return name switch 
        { 
            "sin" => Math.Sin(arguments[0]),
            "cos" => Math.Cos(arguments[0]),
            "abs" => Math.Abs(arguments[0]),
            "sqrt" => Math.Sqrt(arguments[0]),
            "ceiling" => Math.Ceiling(arguments[0]),
            "floor" => Math.Floor(arguments[0]),
            "clamp" => Math.Clamp(arguments[0], arguments[1], arguments[2]),
            "exp" => Math.Exp(arguments[0]),
            "max" => Math.Max(arguments[0], arguments[1]),
            "min" => Math.Min(arguments[0], arguments[1]),
            "log" => Math.Log(arguments[0]),
            "round" => Math.Round(arguments[0]),
            "sign" => Math.Sign(arguments[0]),
            _ => UnimplementedFunction(name)
        };
    }

    private double UnimplementedFunction(string name)
    {
        Utils.Logger.Error(mApi, this, $"Math function '{name}' is not implemented. Implemented functions: sin, cos, abs, sqrt, ceiling, floor, clamp, exp, max, min, log, round, sign.");
        return 0;
    }
}