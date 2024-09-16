using RimThreaded.Patching;

namespace RimThreaded.Patches.RimWorldPatches;

[HarmonyPatch(typeof(AlertsReadout))]
public static class AlertsReadout_Patch
{
    [HarmonyPrefix]
    [PatchCategory(RimThreadedHarmony.NonDestructiveCategory)]
    [HarmonyPatch(nameof(AlertsReadout.AlertsReadoutUpdate))]
    public static bool AlertsReadoutUpdate_Prefix(AlertsReadout __instance)
    {
        // This will disable alert checks on ultrafast speed for an added speed boost.
        return !(Find.TickManager.curTimeSpeed == TimeSpeed.Ultrafast && RimThreadedSettings.Instance.DisableSomeAlerts);
    }
}