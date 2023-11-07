using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace MaltiezFSM.Additional
{
    public class ExtendedDamageSource : DamageSource, IResistibleDamage
    {
        public DamageSourceCallback callback = null;
        public HashSet<IResistance> resistancesToBypass = new();

        bool IResistibleDamage.Bypass(IResistance resist, float damage)
        {
            return resistancesToBypass.Contains(resist);
        }

        void IResistibleDamage.ResistCallback(IResistance resist, DamageSource damageSource, ref float damage, Entity receiver)
        {
            if (callback != null) callback(resist, damageSource, ref damage, receiver);
        }
    }
}
