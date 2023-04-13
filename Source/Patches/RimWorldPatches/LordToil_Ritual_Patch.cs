using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RimThreaded.Patches.RimWorldPatches
{
    [HarmonyPatch(typeof(LordToil_Ritual))]
    public static class LordToil_Ritual_Patch
    {
        [RebindFieldPatch]
        public static ThreadLocal<List<LocalTargetInfo>> reservedThings = new(() => new());

        [RebindFieldPatch]
        public static ThreadLocal<List<CachedPawnRitualDuty>> cachedDuties = new(() => new());
    }
}
