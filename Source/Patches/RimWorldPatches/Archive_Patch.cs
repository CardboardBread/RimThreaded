using RimThreaded.Patching;
using RimWorld;
using System;
using Verse;

namespace RimThreaded.Patches.RimWorldPatches
{
    [HarmonyPatch(typeof(Archive))]
    public static class Archive_Patch
    {
        [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
        [RequireLockPatch(nameof(Archive.ExposeData))]
        [RequireLockPatch(nameof(Archive.Add))]
        [RequireLockPatch(nameof(Archive.Remove))]
        [RequireLockPatch(nameof(Archive.Contains))]
        public static class InstanceLocks
        {
        }
    }
}
