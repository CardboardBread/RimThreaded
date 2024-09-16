using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;
using System;
using HarmonyLib;
using RimThreaded.Patching;

namespace RimThreaded.Patches.RimWorldPatches;

[HarmonyPatch(typeof(TimeControls))]
public class TimeControls_Patch
{
    public static bool lastTickForcedSlow;
    public static bool overrideForcedSlow;

    [HarmonyPrefix]
    [PatchCategory(RimThreadedHarmony.NonDestructiveCategory)]
    [HarmonyPatch(nameof(TimeControls.DoTimeControlsGUI))]
    public static bool DoTimeControlsGUI(Rect timerRect)
    {
        Method(timerRect);
        return true;

        static void Method(Rect timerRect)
        {
            TickManager tickManager = Find.TickManager;

            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }

            if (!Find.WindowStack.WindowsForcePause)
            {
                if (KeyBindingDefOf.TimeSpeed_Fast.KeyDownEvent ||
                    KeyBindingDefOf.TimeSpeed_Superfast.KeyDownEvent ||
                    KeyBindingDefOf.TimeSpeed_Ultrafast.KeyDownEvent)
                {
                    if (lastTickForcedSlow)
                    {
                        overrideForcedSlow = true;
                    }
                }
            }

            // Allow speed 4 even if not dev mode.
            if (!Prefs.DevMode)
            {
                if (KeyBindingDefOf.TimeSpeed_Ultrafast.KeyDownEvent)
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                    TimeControls.PlaySoundOf(tickManager.CurTimeSpeed);
                    Event.current.Use();
                }

                if (KeyBindingDefOf.Dev_TickOnce.KeyDownEvent && tickManager.CurTimeSpeed == TimeSpeed.Paused)
                {
                    tickManager.DoSingleTick();
                    SoundDefOf.Clock_Stop.PlayOneShotOnCamera();
                }
            }
        }
    }

}