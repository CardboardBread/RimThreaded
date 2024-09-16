using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RimThreaded.Patches.RimWorldPatches;

[HarmonyPatch(typeof(LordJob_Ritual))]
public static class LordJob_Ritual_Patch
{
    [RebindFieldPatch(nameof(LordJob_Ritual.totalPresenceTmp))]
    public static ThreadLocal<Dictionary<Pawn, int>> totalPresenceTmp = new(() => new());
}