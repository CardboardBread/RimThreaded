using HarmonyLib;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Verse;

namespace RimThreaded.Patches.VersePatches
{
    [HarmonyPatch(typeof(Dijkstra<>))]
    public static class Dijkstra_Patch<T>
    {
        [RebindFieldPatch] public static ThreadLocal<Dictionary<T, float>> distances = new(() => new());
        [RebindFieldPatch] public static ThreadLocal<FastPriorityQueue<KeyValuePair<T, float>>> queue = new(() => new(new Dijkstra<T>.DistanceComparer()));
        [RebindFieldPatch] public static ThreadLocal<List<T>> singleNodeList = new(() => new());
        [RebindFieldPatch] public static ThreadLocal<List<KeyValuePair<T, float>>> tmpResult = new(() => new());

        // As it turns out, the whole original patch was to make the static fields [ThreadStatic] and to replace all the
        // static methods with ones that reference the new fields.
    }
}
