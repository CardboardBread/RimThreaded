using HarmonyLib;
using Verse;

namespace RimThreaded.Utilities;

/// <summary>
/// Singleton class for adding hooks into points along game init
/// </summary>
[HarmonyPatch]
public static class Patch_Checkpoints
{
    [HarmonyPostfix, HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadAllActiveMods))]
    internal static void PostModLoad()
    {
        
    }
}