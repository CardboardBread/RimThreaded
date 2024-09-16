using HarmonyLib;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimThreaded.Patches.VersePatches;

[HarmonyPatch(typeof(SimplePool<>))]
public static class SimplePool_Patch<T> where T : new()
{
    private static ConcurrentStack<T> FreeItems = new();

    public static int FreeItemsCount => FreeItems.Count;

    public static T Get() => FreeItems.TryPop(out T freeItem) ? freeItem : new T();

    // TODO: as a precaution this might require a check for duplicates.
    public static void Return(T item) => FreeItems.Push(item);

    // Replaces RimThreadedHarmony.SimplePool_Patch_RunNonDestructivePatches()
    [HarmonyPatch]
    public static class ReversePatches
    {
        internal static IEnumerable<string> _GetMethodNames()
        {
            yield return MethodGroups.AsInfo(SimplePool<int>.Get).Name;
            yield return MethodGroups.AsInfo(SimplePool<int>.Return).Name;
            yield return AccessTools.PropertyGetter(typeof(SimplePool<>), nameof(SimplePool<int>.FreeItemsCount)).Name;
        }

        internal static IEnumerable<Type> _GetGenericTypes()
        {
            yield return typeof(List<float>);
            yield return typeof(List<Pawn>);
            yield return typeof(List<Sustainer>);
            yield return typeof(List<IntVec3>);
            yield return typeof(List<Thing>);
            yield return typeof(List<Gizmo>);
            yield return typeof(List<Hediff>);
            yield return typeof(HashSet<IntVec3>);
            yield return typeof(HashSet<Pawn>);
            yield return typeof(Job);
            yield return typeof(Toil);
            yield return typeof(RegionProcessorClosestThingReachable);
        }

        [PatchCategory("NonDestructive")]
        [ReplacePatchesSource]
        public static IEnumerable<(MethodBase, HarmonyMethod, MethodInfo)> GetReversePatches()
        {
            foreach (var genericType in _GetGenericTypes())
            {
                var sourceType = typeof(SimplePool<>).MakeGenericType(genericType);
                var targetType = typeof(SimplePool_Patch<>).MakeGenericType(genericType);

                foreach (var methodName in _GetMethodNames())
                {
                    var sourceMethod = AccessTools.Method(sourceType, methodName) ?? methodNull();
                    var targetMethod = AccessTools.Method(targetType, methodName) ?? methodNull();

                    yield return (sourceMethod, new(targetMethod), null);

                    static MethodInfo methodNull() => throw new ArgumentNullException($"{nameof(sourceType)} & {nameof(methodName)}");
                }
            }
        }
    }
}