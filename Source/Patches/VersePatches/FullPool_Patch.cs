using HarmonyLib;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimThreaded.Patches.VersePatches;

public static class FullPool_Patch<TPoolable> where TPoolable : IFullPoolable, new()
{
    private static readonly ConcurrentStack<TPoolable> FreeItems = new();

    public static TPoolable Get()
    {
        return !FreeItems.TryPop(out var freeItem) ? new() : freeItem;
    }

    public static void Return(TPoolable item)
    {
        item.Reset();
        FreeItems.Push(item);
    }

    // Replaces RimThreadedHarmony.FullPool_Patch_RunNonDestructivePatches()
    [HarmonyPatch]
    public static class ReplacePatches
    {
        [PatchCategory("NonDestructive")]
        [ReplacePatchesSource]
        public static IEnumerable<(MethodBase, HarmonyMethod, MethodInfo)> GetReplacePatches()
        {
            var original = typeof(FullPool<PawnStatusEffecters.LiveEffecter>);
            var patched = typeof(FullPool_Patch<PawnStatusEffecters.LiveEffecter>);
            var getName = nameof(FullPool<PawnStatusEffecters.LiveEffecter>.Get);
            var returnName = nameof(FullPool_Patch<PawnStatusEffecters.LiveEffecter>.Return);
            yield return (AccessTools.Method(original, getName), new(AccessTools.Method(patched, getName)), null);
            yield return (AccessTools.Method(original, returnName), new(AccessTools.Method(patched, returnName)), null);
        }
    }
}