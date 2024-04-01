using MaltiezFSM.Framework;
using SimpleExpressionEngine;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace MaltiezFSM.Framework;

public class ValueModifier
{
    private readonly INode<float, float, float> mFormula;

    public ValueModifier(ICoreAPI api, string formula)
    {
        mFormula = MathParser.Parse(formula);
    }

    public float Calc(float value, System.Func<string, float> function)
    {
        return mFormula.Evaluate(new CombinedContext<float, float>(new List<IContext<float, float>>() { new ValueContext(value), new MathContext() }));
    }
}