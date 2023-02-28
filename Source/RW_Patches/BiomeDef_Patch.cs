﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimThreaded.Utilities;
using RimWorld;
using Verse;

namespace RimThreaded.RW_Patches
{
    public class BiomeDef_Patch
    {
        [DestructivePatch]
        internal static void RunDestructivePatches()
        {
            RimThreadedHarmony.Prefix(original: typeof(BiomeDef),
                                      patched: typeof(BiomeDef_Patch),
                                      methodName: nameof(BiomeDef.CachePlantCommonalitiesIfShould));
        }

        public static bool CachePlantCommonalitiesIfShould(BiomeDef __instance)
        {
            if (__instance.cachedPlantCommonalities != null)
            {
                return false;
            }
            if (__instance.cachedPlantCommonalities != null)
            {
                return false;
            }
            lock (__instance) //TODO more efficient lock
            {
                Dictionary<ThingDef, float> localCachedPlantCommonalities = new Dictionary<ThingDef, float>();
                for (int i = 0; i < __instance.wildPlants.Count; i++)
                {
                    BiomePlantRecord wildPlant = __instance.wildPlants[i];
                    ThingDef plant = wildPlant.plant;
                    if (plant != null)
                    {
                        localCachedPlantCommonalities[plant] = wildPlant.commonality;
                    }
                }

                foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
                {
                    if (allDef.plant == null || allDef.plant.wildBiomes == null) continue;
                    for (int j = 0; j < allDef.plant.wildBiomes.Count; j++)
                    {
                        if (allDef.plant.wildBiomes[j].biome == __instance)
                        {
                            localCachedPlantCommonalities.Add(allDef, allDef.plant.wildBiomes[j].commonality);
                        }
                    }
                }
                __instance.cachedPlantCommonalitiesSum = localCachedPlantCommonalities.Sum(x => x.Value);
                __instance.cachedPlantCommonalities = localCachedPlantCommonalities;

            }
            return false;
        }

    }
}
