﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimThreaded.Patches.RimWorldPatches;

public class ResourceCounter_Patch
{
    public static object lockObject = new object();

    internal static void RunDestructivePatches()
    {
        Type original = typeof(ResourceCounter);
        Type patched = typeof(ResourceCounter_Patch);
        RimThreadedHarmony.Prefix(original, patched, "ResetDefs");
        RimThreadedHarmony.Prefix(original, patched, "ResetResourceCounts");
        RimThreadedHarmony.Prefix(original, patched, "GetCount"); //maybe not needed
        RimThreadedHarmony.Prefix(original, patched, "UpdateResourceCounts"); //maybe not needed
        RimThreadedHarmony.Prefix(original, patched, "get_TotalHumanEdibleNutrition"); //maybe not needed
    }

    public static bool get_TotalHumanEdibleNutrition(ResourceCounter __instance, ref float __result)
    {
        float num = 0f;
        lock (lockObject)
        {
            Dictionary<ThingDef, int> snapshotCountedAmounts = __instance.countedAmounts;
            foreach (KeyValuePair<ThingDef, int> countedAmount in snapshotCountedAmounts)
            {
                if (countedAmount.Key.IsNutritionGivingIngestible && countedAmount.Key.ingestible.HumanEdible)
                {
                    num += countedAmount.Key.GetStatValueAbstract(StatDefOf.Nutrition) * countedAmount.Value;
                }
            }
        }

        __result = num;
        return false;
    }

    public static bool ResetDefs()
    {
        lock (lockObject)
        {
            ResourceCounter.resources = new List<ThingDef>(from def in DefDatabase<ThingDef>.AllDefs
                where def.CountAsResource
                orderby def.resourceReadoutPriority descending
                select def);
        }
        return false;
    }

    public static bool ResetResourceCounts(ResourceCounter __instance)
    {
        lock (lockObject)
        {
            Dictionary<ThingDef, int> newCountedAmounts = new Dictionary<ThingDef, int>();
            List<ThingDef> tempResources = ResourceCounter.resources;
            for (int i = 0; i < tempResources.Count; i++)
            {
                newCountedAmounts.Add(tempResources[i], 0);
            }
            __instance.countedAmounts = newCountedAmounts;
        }

        return false;
    }

    public static bool GetCount(ResourceCounter __instance, ref int __result, ThingDef rDef)
    {
        if (rDef.resourceReadoutPriority == ResourceCountPriority.Uncounted)
        {
            __result = 0;
            return false;
        }

        lock (lockObject)
        {
            if (__instance.AllCountedAmounts.TryGetValue(rDef, out int value))
            {
                __result = value;
                return false;
            }

            Dictionary<ThingDef, int> newCountedAmounts = new Dictionary<ThingDef, int>(__instance.AllCountedAmounts);
            Log.Error("Looked for nonexistent key " + rDef + " in counted resources.");
            newCountedAmounts.Add(rDef, 0);
            __instance.countedAmounts = newCountedAmounts;
        }
        __result = 0;
        return false;
    }
    public static bool UpdateResourceCounts(ResourceCounter __instance)
    {
        lock (lockObject)
        {
            __instance.ResetResourceCounts();
            Dictionary<ThingDef, int> newCountedAmounts = new Dictionary<ThingDef, int>(__instance.AllCountedAmounts);
            bool changed = false;
            List<SlotGroup> allGroupsListForReading = __instance.map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < allGroupsListForReading.Count; i++)
            {
                foreach (Thing heldThing in allGroupsListForReading[i].HeldThings)
                {
                    Thing innerIfMinified = heldThing.GetInnerIfMinified();
                    if (innerIfMinified.def.CountAsResource && !innerIfMinified.IsNotFresh())
                    {
                        newCountedAmounts[innerIfMinified.def] += innerIfMinified.stackCount;
                        changed = true;
                    }
                }
            }
            if (changed)
            {
                __instance.countedAmounts = newCountedAmounts;
            }
        }
        return false;
    }

}