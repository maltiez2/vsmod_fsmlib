using SimpleExpressionEngine;
using System;
using Vintagestory.API.Common;

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

public class StatsContext : BaseContext
{
    private readonly IPlayer mPlayer;
    private readonly float mValue;

    public StatsContext(ICoreAPI api, IPlayer player, float value) : base(api)
    {
        mPlayer = player;
        mValue = value;
    }

    public override double ResolveVariable(string name)
    {
        return name switch
        {
            "value" => mValue,
            "PI" => Math.PI,
            "E" => Math.E,
            _ => mPlayer.Entity.Stats.GetBlended(name)
        };
    }
}