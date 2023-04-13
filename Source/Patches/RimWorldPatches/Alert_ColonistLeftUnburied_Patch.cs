using RimThreaded.Patching;
using System.Threading;

namespace RimThreaded.Patches.RimWorldPatches
{
    [HarmonyPatch(typeof(Alert_ColonistLeftUnburied))]
    public static class Alert_ColonistLeftUnburied_Patch
    {
        [RebindFieldPatch]
        public static ThreadLocal<List<Thing>> unburiedColonistCorpsesResult = new(() => new());

        // Replace original method with null-checking version.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Alert_ColonistLeftUnburied.IsCorpseOfColonist))]
        public static bool IsCorpseOfColonist_Replace(ref bool __result, Corpse corpse)
        {
            __result = corpse?.InnerPawn?.Faction == Faction.OfPlayer
                       && (corpse?.InnerPawn?.def?.race?.Humanlike ?? false)
                       && (!corpse?.InnerPawn?.IsQuestLodger() ?? false)
                       && (!corpse?.InnerPawn?.IsSlave ?? false)
                       && (!corpse?.IsInAnyStorage() ?? false);
            return false;
        }
    }
}
