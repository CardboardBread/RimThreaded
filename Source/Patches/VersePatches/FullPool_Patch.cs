using HarmonyLib;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimThreaded.Patches.VersePatches
{
    public static class FullPool_Patch<T> where T : IFullPoolable, new()
    {
        private static ConcurrentStack<T> FreeItems = new();

        public static T Get()
        {
            return !FreeItems.TryPop(out T freeItem) ? new T() : freeItem;
        }

        public static void Return(T item)
        {
            item.Reset();
            FreeItems.Push(item);
        }

        // Replaces RimThreadedHarmony.FullPool_Patch_RunNonDestructivePatches()
        [HarmonyPatch]
        public static class ReversePatches
        {
            [PatchCategory("NonDestructive")]
            [ReplacePatchesSource]
            public static IEnumerable<(MethodBase, HarmonyMethod, MethodInfo)> GetReversePatches()
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
}
