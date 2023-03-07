﻿using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimThreaded.RW_Patches
{
    [HarmonyPatch(typeof(Alert_MinorBreakRisk))]
    class Alert_MinorBreakRisk_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Alert_MinorBreakRisk.GetReport))]
        public static bool GetReport(Alert_MinorBreakRisk __instance, ref AlertReport __result)
        {
            List<Pawn> pawnsAtRiskMinorResult = new List<Pawn>();
            List<Pawn> pawnList = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists_NoCryptosleep;
            for (int i = 0; i < pawnList.Count; i++)
            {
                Pawn item = pawnList[i];
                if (item.Downed || item.MentalStateDef != null) continue;
                float curMood = item.mindState.mentalBreaker.CurMood;
                if (curMood < item.mindState.mentalBreaker.BreakThresholdMajor)
                {
                    return false;
                }
                if (curMood < item.mindState.mentalBreaker.BreakThresholdMinor)
                {
                    pawnsAtRiskMinorResult.Add(item);
                }
            }
            __result = AlertReport.CulpritsAre(pawnsAtRiskMinorResult);
            return false;
        }
    }
}
