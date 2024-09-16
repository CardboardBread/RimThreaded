﻿using UnityEngine;
using System;
using Verse;
using static RimThreaded.RimThreaded;
using static System.Threading.Thread;

namespace RimThreaded.Patches.VersePatches;

public class MeshMakerShadows_Patch
{
    internal static void RunDestructivePatches()
    {
        Type original = typeof(MeshMakerShadows);
        Type patched = typeof(MeshMakerShadows_Patch);
        RimThreadedHarmony.Prefix(original, patched, "NewShadowMesh", new Type[] { typeof(float), typeof(float), typeof(float) });
    }

    static readonly Func<object[], object> FuncNewShadowMesh = parameters =>
        MeshMakerShadows.NewShadowMesh(
            (float)parameters[0],
            (float)parameters[1],
            (float)parameters[2]);

    public static bool NewShadowMesh(ref Mesh __result, float baseWidth, float baseHeight, float tallness)
    {
        if (!CurrentThread.IsBackground || !allWorkerThreads.TryGetValue(CurrentThread, out ThreadState threadInfo))
            return true;
        threadInfo.safeFunctionRequest = new object[] { FuncNewShadowMesh, new object[] { baseWidth, baseHeight, tallness } };
        MainWaitHandle.Set();
        threadInfo.eventWaitStart.WaitOne();
        __result = (Mesh)threadInfo.safeFunctionResult;
        return false;
    }

}