using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimThreaded.Patching;
using RimThreaded.Properties;
using Verse;

namespace RimThreaded.Utilities;

public static class PatchConflictUtility
{
    /// <summary>Tuple-like type for modelling potential conflicts between RimThreaded Harmony patches and foreign Harmony patches.</summary>
    /// <param name="Original">The method/constructor/getter/setter that a Harmony patch conflict was detected on.</param>
    /// <param name="Local">The RimThreaded Harmony patch that has the potential to conflict with foreign Harmony patches.</param>
    /// <param name="ForeignPatches">A collection of foreign Harmony patches that may conflict with a RimThreaded Harmony patch.</param>
    public record struct PatchConflicts(MethodBase Original, Patch Local, Patch[] ForeignPatches);

    public static string PatchConflictsText { get; private set; }

    private static IEnumerable<PatchConflicts> GetConflictingPatches(MethodBase original)
    {
        var patches = Harmony.GetPatchInfo(original);
        if (patches is null) yield break;
        if (!patches.AllPatches().Any()) yield break;
        
        // var owners = patches.AllPatches().Select(p => p.owner).Distinct();

        var hasLocalPatches = patches.AllPatches().Any(IsLocalPatch);
        var hasForeignPatches = patches.AllPatches().Any(IsForeignPatch);
        
        if (!hasLocalPatches) yield break;
        if (hasLocalPatches && !hasForeignPatches) yield break;
        
        foreach (var conflictingPrefix in patches.Prefixes.Where(IsPossibleConflictingPatch))
        {
            var higherPriorityForeignPrefixes = patches.Prefixes
                .Where(IsForeignPatch)
                .Where(pre => pre.priority >= conflictingPrefix.priority)
                .ToArray();
            
            if (higherPriorityForeignPrefixes.Any())
            {
                yield return new(original, conflictingPrefix, higherPriorityForeignPrefixes);
            }
        }

        foreach (var conflictingPostfix in patches.Postfixes.Where(IsPossibleConflictingPatch))
        {
            var higherPriorityForeignPostfixes = patches.Postfixes
                .Where(IsForeignPatch)
                .Where(post => post.priority >= conflictingPostfix.priority)
                .ToArray();
            
            if (higherPriorityForeignPostfixes.Any())
            {
                yield return new(original, conflictingPostfix, higherPriorityForeignPostfixes);
            }
        }

        foreach (var conflictingTranspiler in patches.Transpilers.Where(IsPossibleConflictingPatch))
        {
            var competingTranspilers = patches.Transpilers
                .Where(trans => !Equals(trans, conflictingTranspiler))
                .ToArray();
            
            if (competingTranspilers.Any())
            {
                yield return new(original, conflictingTranspiler, competingTranspilers);
            }
        }
    }

    private static IEnumerable<Patch> AllPatches(this HarmonyLib.Patches patches) =>
        patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers);

    private static (int highest, int lowest) GetPriorityLimits(IEnumerable<Patch> conflictingPrefixes)
    {
        var highestPriority = Priority.Last;
        var lowestPriority = Priority.First;

        foreach (var prefix in conflictingPrefixes)
        {
            if (prefix.priority > highestPriority)
            {
                highestPriority = prefix.priority;
            }

            if (prefix.priority < lowestPriority)
            {
                lowestPriority = prefix.priority;
            }
        }

        return (highestPriority, lowestPriority);
    }

    private static bool IsPossibleConflictingPatch(this Patch patch) =>
        IsLocalPatch(patch) && patch.PatchMethod.HarmonyPatchCategory() == RimThreadedHarmony.DestructiveCategory;

    private static bool IsForeignPatch(this Patch patch) => patch.owner != RimThreadedHarmony.Instance.Id;

    private static bool IsLocalPatch(this Patch patch) => patch.owner == RimThreadedHarmony.Instance.Id;

    private static string GetPatchConflictsText(ICollection<PatchConflicts> conflicts)
    {
        var patchCount = Resources.PatchConflictUtility_GetPatchConflictsText_conflictsCount;
        var patchHeader = Resources.PatchConflictUtility_GetPatchConflictsText_HarmonyOriginal;
        var patchSubheader = Resources.PatchConflictUtility_GetPatchConflictsText_HarmonyPatch;
        var patchBody = Resources.PatchConflictUtility_GetPatchConflictsText_PatchDescription;

        var builder = new StringBuilder(MinimumTextLength());
        builder.AppendLine(string.Format(patchCount, conflicts.Count));
        
        foreach (var patch in conflicts)
        {
            builder.AppendLine(string.Format(patchHeader, patch.Original.FullDescription()));
            builder.AppendLine(string.Format(patchSubheader, patch.Local.PatchMethod.FullDescription(), patch.Local.priority));
            
            foreach (var foreign in patch.ForeignPatches)
            {
                builder.AppendLine(string.Format(patchBody, foreign.patchMethod.FullDescription(), foreign.owner, foreign.priority));
            }
        }
        
        return builder.ToString();

        int MinimumTextLength()
        {
            var patchHeaderLength = patchHeader.Length * conflicts.Count;
            var patchSubheaderLength = patchSubheader.Length * conflicts.Count;
            var patchBodyLength = patchBody.Length * conflicts
                .Select(conflict => conflict.ForeignPatches)
                .Select(patches => patches.Length)
                .Sum();
            return patchCount.Length + patchHeaderLength + patchSubheaderLength + patchBodyLength;
        }
    }

    /// <summary>
    /// Search for and report conflicts between Harmony patches in this mod and Harmony patches in other mods.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadAllActiveMods))]
    public static void DiscoverPatchConflicts()
    {
        RTLog.Message("Discovering potential Harmony patch conflicts...");
        var conflictingPatches = Harmony.GetAllPatchedMethods().SelectMany(GetConflictingPatches).AsCollection();
        PatchConflictsText = GetPatchConflictsText(conflictingPatches);
        if (Prefs.LogVerbose && conflictingPatches.Count > 0)
        {
            RTLog.Warning(PatchConflictsText);
        }
    }
}