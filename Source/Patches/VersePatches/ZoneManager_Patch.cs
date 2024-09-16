﻿using RimWorld;
using System;
using Verse;

namespace RimThreaded.Patches.VersePatches;

class ZoneManager_Patch
{
    internal static void RunNonDestructivePatches()
    {
        Type original = typeof(ZoneManager);
        Type patched = typeof(ZoneManager_Patch);
        RimThreadedHarmony.Postfix(original, patched, "AddZoneGridCell");
    }

    public static void AddZoneGridCell(ZoneManager __instance, Zone zone, IntVec3 c)
    {
        if (Current.ProgramState == ProgramState.Playing)
        {
            if (zone is Zone_Growing)
            {
                //Log.Message("Adding growing zone cell to awaiting plant cells");
                JumboCell.ReregisterObject(zone.Map, c, RimThreaded.plantSowing_Cache);
            }
        }
    }

}