using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimThreaded.StaticReplacement;
using RimThreaded.Utilities;
using RimWorld;
using Verse;

namespace RimThreaded.RW_Patches
{
    [RequireLock]
    [HarmonyPatch(typeof(IdeoManager))]
    class IdeoManager_Patch
    {
        [ReplaceField]
        [ThreadStatic]
        public static List<LordJob_Ritual> activeRitualsTmp = new();

        [HarmonyReversePatch]
        [PatchCategory("Destructive")]
        [HarmonyPatch(nameof(IdeoManager.IdeoManagerTick))]
        public static void IdeoManagerTick(IdeoManager __instance, ref List<Ideo> ___ideos, ref List<Ideo> ___toRemove)
        {
            GenAsync.InlineForEach(___ideos, i => i.IdeoTick());
            GenAsync.InlineForEach(___toRemove, i => __instance.Remove(i));
            ___toRemove.Clear();
        }
    }
}
