using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using RimWorld;
using Verse;

namespace RimThreaded.Patches.RimWorldPatches
{
    [RequireLockPatch]
    [HarmonyPatch(typeof(IdeoManager))]
    public static class IdeoManager_Patch
    {
        [RebindFieldPatch]
        public static ThreadLocal<List<LordJob_Ritual>> activeRitualsTmp = new(() => new());

        [RebindFieldPatch]
        public static ThreadLocal<List<Faction>> npcWithIdeoTmp = new(() => new());

        [HarmonyPrefix]
        [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
        [HarmonyPatch(nameof(IdeoManager.IdeoManagerTick))]
        public static bool IdeoManagerTick_Replace(IdeoManager __instance, ref List<Ideo> ___ideos, ref List<Ideo> ___toRemove)
        {
            Parallel.ForEach(___ideos, ideo => ideo.IdeoTick());
            Parallel.ForEach(___toRemove, ideo => __instance.Remove(ideo));
            ___toRemove.Clear();
            return false;
        }
    }
}
