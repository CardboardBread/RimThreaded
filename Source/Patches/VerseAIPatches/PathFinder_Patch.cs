using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;
using static Verse.AI.PathFinder;
using System.Linq;
using RimThreaded.Utilities;
using System.Diagnostics.Eventing.Reader;

namespace RimThreaded.Patches.VerseAIPatches
{
    [HarmonyPatch(typeof(PathFinder))]
    public static class PathFinder_Patch
    {
        [ThreadStatic] public static List<int> disallowedCornerIndices;
        [ThreadStatic] public static PathFinderNodeFast[] calcGrid;
        [ThreadStatic] public static PriorityQueue<int, int> openList;
        [ThreadStatic] public static ushort statusOpenValue;
        [ThreadStatic] public static ushort statusClosedValue;
        [ThreadStatic] public static Dictionary<PathFinder, RegionCostCalculatorWrapper> regionCostCalculatorDict;

        [ThreadStaticInitializer]
        public static void InitializeThreadStatics()
        {
            //openList = new FastPriorityQueue<CostNode>(new CostNodeComparer());
            openList = new PriorityQueue<int, int>();
            statusOpenValue = 1;
            statusClosedValue = 2;
            disallowedCornerIndices = new List<int>(4);
            regionCostCalculatorDict = new Dictionary<PathFinder, RegionCostCalculatorWrapper>();
        }

        [PatchCategoryAttribute("NonDestructive")]
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PathFinder.InitStatusesAndPushStartNode))]
        public static bool InitStatusesAndPushStartNode(PathFinder __instance, ref int curIndex, IntVec3 start)
        {
            int size = __instance.mapSizeX * __instance.mapSizeZ;
            if (calcGrid == null || calcGrid.Length < size)
            {
                calcGrid = new PathFinderNodeFast[size];
            }
            return true;
        }

        [PatchCategoryAttribute("Destructive")]
        [FieldPatch(nameof(PathFinder.regionCostCalculator), PatchType = FieldPatchType.Load)] // interpreted as replace all field loads with calling this method
        public static RegionCostCalculatorWrapper GetRegionCostCalculator(PathFinder __instance)
        {
            if (!regionCostCalculatorDict.TryGetValue(__instance, out RegionCostCalculatorWrapper regionCostCalculatorWrapper))
            {
                regionCostCalculatorWrapper = new RegionCostCalculatorWrapper(__instance.map);
                regionCostCalculatorDict[__instance] = regionCostCalculatorWrapper;
            }
            return regionCostCalculatorWrapper;
        }

        [PatchCategoryAttribute("Destructive")]
        [FieldPatch(nameof(PathFinder.regionCostCalculator), PatchType = FieldPatchType.Store)] // interpreted as replace all field stores with calling this method
        public static void SetRegionCostCalculator(PathFinder __instance, RegionCostCalculatorWrapper regionCostCalculatorWrapper)
        {
            regionCostCalculatorDict[__instance] = regionCostCalculatorWrapper;
            return;
        }
    }
}
