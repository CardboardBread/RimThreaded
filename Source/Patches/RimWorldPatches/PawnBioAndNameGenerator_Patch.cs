using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patches.RimWorldPatches;

[HarmonyPatch(typeof(PawnBioAndNameGenerator))]
public static class PawnBioAndNameGenerator_Patch
{
    [RebindFieldPatch]
    public static List<BackstoryDef> tmpBackstories = new List<BackstoryDef>();

    [RebindFieldPatch]
    public static List<string> tmpNames = new List<string>();

    [RebindFieldPatch]
    public static HashSet<string> usedNamesTmp = new HashSet<string>();
}