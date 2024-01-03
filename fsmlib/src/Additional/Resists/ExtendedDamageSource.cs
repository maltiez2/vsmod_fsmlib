using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MaltiezFSM.Additional;

public class ExtendedDamageSource : DamageSource, IResistibleDamage
{
    public DamageSourceCallback? Callback { get; set; } = null;
    public HashSet<IResist> ResistancesToBypass { get; set; } = new();

    bool IResistibleDamage.Bypass(IResist resist, float damage)
    {
        return ResistancesToBypass.Contains(resist);
    }

    void IResistibleDamage.ResistCallback(IResist resist, DamageSource damageSource, ref float damage, Entity receiver)
    {
        Callback?.Invoke(resist, damageSource, ref damage, receiver);
    }
}
