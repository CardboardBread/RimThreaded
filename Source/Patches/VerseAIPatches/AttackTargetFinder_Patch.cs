using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse.AI;

namespace RimThreaded.Patches.VerseAIPatches;

[HarmonyPatch(typeof(AttackTargetFinder))]
public static class AttackTargetFinder_Patch
{
    [RebindFieldPatch]
    public static ThreadLocal<List<IAttackTarget>> tmpTargets = new(() => new(128));

    [RebindFieldPatch]
    public static ThreadLocal<List<Pair<IAttackTarget, float>>> availableShootingTargets = new(() => new());

    [RebindFieldPatch]
    public static ThreadLocal<List<float>> tmpTargetScores = new(() => new());

    [RebindFieldPatch]
    public static ThreadLocal<List<bool>> tmpCanShootAtTarget = new(() => new());

    [RebindFieldPatch]
    public static ThreadLocal<List<IntVec3>> tempDestList = new(() => new());

    [RebindFieldPatch]
    public static ThreadLocal<List<IntVec3>> tempSourceList = new(() => new());
}