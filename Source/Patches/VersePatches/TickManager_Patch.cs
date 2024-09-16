using RimThreaded.Patching;
using System.Threading;

namespace RimThreaded.Patches.VersePatches;

[HarmonyPatch(typeof(TickManager))]
public static class TickManager_Patch
{
    // We use Parallel.Invoke to group ticks into parallel 'regions'
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(TickManager.DoSingleTick))]
    public static bool DoSingleTick_Replace(TickManager __instance, ref int ___ticksGameInt, ref int ___TicksGame, ref int ___lastAutoScreenshot, ref TickList ___tickListNormal, ref TickList ___tickListRare, ref TickList ___tickListLong)
    {
        Monitor.Enter(__instance);
        RimThreaded.ForEach(Find.Maps, map => map.MapPreTick());

        lock (typeof(DebugSettings))
        {
            if (DebugSettings.fastEcology)
            {
                ___ticksGameInt++;
            }
            else
            {
                ___ticksGameInt += 2000;
            }
        }

        lock (typeof(Shader))
        {
            Shader.SetGlobalFloat(ShaderPropertyIDs.GameSeconds, ___TicksGame.TicksToSeconds());
        }

        RimThreaded.Invoke(___tickListNormal.Tick,
            ___tickListRare.Tick,
            ___tickListLong.Tick,
            Find.DateNotifier.DateNotifierTick,
            Find.Scenario.TickScenario,
            Find.World.WorldTick,
            Find.StoryWatcher.StoryWatcherTick,
            Find.GameEnder.GameEndTick,
            Find.Storyteller.StorytellerTick,
            Find.TaleManager.TaleManagerTick,
            Find.QuestManager.QuestManagerTick,
            Find.World.WorldPostTick);

        RimThreaded.ForEach(Find.Maps, map => map.MapPostTick());

        RimThreaded.Invoke(Find.History.HistoryTick,
            GameComponentUtility.GameComponentTick,
            Find.LetterStack.LetterStackTick,
            Find.Autosaver.AutosaverTick);

        lock (typeof(DebugViewSettings))
        {
            lock (typeof(ScreenshotTaker))
            {
                if (DebugViewSettings.logHourlyScreenshot && Find.TickManager.TicksGame >= ___lastAutoScreenshot + 2500)
                {
                    ScreenshotTaker.QueueSilentScreenshot();
                    ___lastAutoScreenshot = Find.TickManager.TicksGame / 2500 * 2500;
                }
            }
        }
            
        RimThreaded.Invoke(FilthMonitor.FilthMonitorTick,
            Find.TransportShipManager.ShipObjectsTick);

        lock (typeof(UnityDebug)) UnityDebug.developerConsoleVisible = false;
        Monitor.Exit(__instance);
        return false;
    }

    [HarmonyPrefix]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(TickManager.TickRateMultiplier), MethodType.Getter)]
    public static bool TickRateMultiplier_Replace(TickManager __instance, ref float __result)
    {
        __result = Inner(__instance);
        return false;

        static float Inner(TickManager @this)
        {
            if (@this.slower.ForcedNormalSpeed && !RimThreadedSettings.Instance.DisableForcedSlowdowns)
            {
                TimeControls_Patch.lastTickForcedSlow = true;
                if (!TimeControls_Patch.overrideForcedSlow)
                {
                    if (@this.curTimeSpeed == TimeSpeed.Paused)
                    {
                        return 0f;
                    }

                    return 1f;
                }
            }
            else
            {
                TimeControls_Patch.lastTickForcedSlow = false;
                TimeControls_Patch.overrideForcedSlow = false;
            }

            switch (@this.curTimeSpeed)
            {
                case TimeSpeed.Paused:
                    return 0f;
                case TimeSpeed.Normal:
                    return RimThreadedSettings.Instance.TimeSpeedNormal;
                case TimeSpeed.Fast:
                    return RimThreadedSettings.Instance.TimeSpeedFast;
                case TimeSpeed.Superfast:
                    if (Find.Maps.Count == 0)
                    {
                        return RimThreadedSettings.Instance.TimeSpeedSuperfast * 2 * 10;
                    }

                    if (@this.NothingHappeningInGame())
                    {
                        return RimThreadedSettings.Instance.TimeSpeedSuperfast * 2;
                    }

                    return RimThreadedSettings.Instance.TimeSpeedSuperfast;
                case TimeSpeed.Ultrafast:
                    if (Find.Maps.Count == 0 || TickManager.UltraSpeedBoost)
                    {
                        return RimThreadedSettings.Instance.TimeSpeedUltrafast * 10;
                    }

                    return RimThreadedSettings.Instance.TimeSpeedUltrafast;
                default:
                    return -1f;
            }
        }
    }
}