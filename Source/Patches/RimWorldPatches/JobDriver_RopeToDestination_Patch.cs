using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patches.RimWorldPatches;

[HarmonyPatch(typeof(JobDriver_RopeToDestination))]
public static class JobDriver_RopeToDestination_Patch
{
    [RebindFieldPatch]
    public static List<Pawn> tmpRopees = new List<Pawn>();
}