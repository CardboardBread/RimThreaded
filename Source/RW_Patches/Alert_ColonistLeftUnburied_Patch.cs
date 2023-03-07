using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace RimThreaded.RW_Patches
{
    [HarmonyPatch(typeof(Alert_ColonistLeftUnburied))]
    class Alert_ColonistLeftUnburied_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Alert_ColonistLeftUnburied.IsCorpseOfColonist))]
        public static bool IsCorpseOfColonist(ref bool __result, Corpse corpse)
        {
            if (corpse == null)
            {
                __result = false;
                return false;
            }
            Pawn InnerPawn = corpse.InnerPawn;
            if (InnerPawn == null)
            {
                __result = false;
                return false;
            }
            ThingDef def = InnerPawn.def;
            if (def == null)
            {
                __result = false;
                return false;
            }
            RaceProperties race = def.race;
            if (race == null)
            {
                __result = false;
                return false;
            }
            __result = InnerPawn.Faction == Faction.OfPlayer && race.Humanlike && !InnerPawn.IsQuestLodger() && !InnerPawn.IsSlave && !corpse.IsInAnyStorage();
            return false;
        }
    }
}
