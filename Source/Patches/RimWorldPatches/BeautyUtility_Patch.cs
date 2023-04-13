using RimThreaded.Patching;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using static RimWorld.BeautyUtility;

namespace RimThreaded.Patches.RimWorldPatches
{
    [HarmonyPatch(typeof(BeautyUtility))]
    public static class BeautyUtility_Patch
    {
        [RebindFieldPatch]
        public static List<IntVec3> beautyRelevantCells = new List<IntVec3>();

        [RebindFieldPatch]
        public static List<Room> visibleRooms = new List<Room>();

        [RebindFieldPatch]
        public static List<Thing> tempCountedThings = new List<Thing>();

        internal static void RunDestructivePatches()
        {
            Type original = typeof(BeautyUtility);
            Type patched = typeof(BeautyUtility_Patch);
            RimThreadedHarmony.Prefix(original, patched, nameof(CellBeauty));
        }
        public static bool CellBeauty(ref float __result, IntVec3 c, Map map, List<Thing> countedThings = null)
        {
            float num = 0.0f;
            float num2 = 0.0f;
            bool flag = false;
            if (map == null) //added
            {
                __result = 0f;
                return false;
            }
            TerrainGrid terrainGrid = map.terrainGrid;
            if (terrainGrid == null) //added
            {
                __result = 0f;
                return false;
            }
            TerrainDef terrainDef = terrainGrid.TerrainAt(c);
            ThingGrid thingGrid = map.thingGrid;
            if (thingGrid == null) //added
            {
                __result = 0f;
                return false;
            }
            bool flag2 = c.GetRoom(map)?.PsychologicallyOutdoors ?? true;
            List<Thing> thingList = thingGrid.ThingsListAt(c); //changed
            for (int index = 0; index < thingList.Count; ++index)
            {
                Thing thing = thingList[index];
                if (!BeautyRelevant(thing.def.category))
                    continue;
                if (countedThings == null)
                    continue;
                if (countedThings.Contains(thing))
                    continue;
                countedThings.Add(thing);
                SlotGroup slotGroup = thing.GetSlotGroup();
                if (slotGroup != null && slotGroup.parent != thing && slotGroup.parent.IgnoreStoredThingsBeauty)
                    continue;
                float num3 = flag2 && thing.def.StatBaseDefined(StatDefOf.BeautyOutdoors) ? thing.GetStatValue(StatDefOf.BeautyOutdoors) : thing.GetStatValue(StatDefOf.Beauty);
                if (thing is Filth && !map.roofGrid.Roofed(c))
                {
                    num3 *= 0.3f;
                }
                if (thing.def.Fillage == FillCategory.Full)
                {
                    flag = true;
                    num2 += num3;
                }
                else
                {
                    num += num3;
                }
            }
            if (flag)
            {
                __result = num2;
                return false;
            }
            if (ModsConfig.BiotechActive && !terrainDef.BuildableByPlayer && c.IsPolluted(map))
            {
                num += -1f;
            }
            if (flag2 && terrainDef.StatBaseDefined(StatDefOf.BeautyOutdoors))
            {
                __result = num + terrainDef.GetStatValueAbstract(StatDefOf.BeautyOutdoors);
                return false;
            }
            __result = num + terrainDef.GetStatValueAbstract(StatDefOf.Beauty);
            return false;
        }
    }
}
