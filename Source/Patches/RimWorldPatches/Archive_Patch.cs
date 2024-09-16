using RimThreaded.Patching;
using RimWorld;
using System;
using HarmonyLib;
using Verse;

namespace RimThreaded.Patches.RimWorldPatches;

[HarmonyPatch(typeof(Archive))]
public static class Archive_Patch
{
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [RequireLockPatch(MemberName = nameof(Archive.ExposeData))]
    [RequireLockPatch(MemberName = nameof(Archive.Add))]
    [RequireLockPatch(MemberName = nameof(Archive.Remove))]
    [RequireLockPatch(MemberName = nameof(Archive.Contains))]
    public static class InstanceLocks
    {
    }
}