﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using static HarmonyLib.AccessTools;
using static RimThreaded.RimThreadedHarmony;

namespace RimThreaded.Patches.RimWorldPlanetPatches;

public class TileTemperaturesComp_Transpile
{
    public static IEnumerable<CodeInstruction> WorldComponentTick(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
    {
        List<CodeInstruction> instructionsList = instructions.ToList();
        int i = 0;
        List<CodeInstruction> loadLockObjectInstructions = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldsfld, Field(typeof(TileTemperaturesComp_Patch), "worldComponentTickLock"))
        };
        LocalBuilder lockObject = iLGenerator.DeclareLocal(typeof(object));
        LocalBuilder lockTaken = iLGenerator.DeclareLocal(typeof(bool));
        foreach (CodeInstruction ci in EnterLock(
                     lockObject, lockTaken, loadLockObjectInstructions, instructionsList[i]))
            yield return ci;
        while (i < instructionsList.Count - 1)
        {
            yield return instructionsList[i++];
        }
        foreach (CodeInstruction ci in ExitLock(
                     iLGenerator, lockObject, lockTaken, instructionsList[i]))
            yield return ci;
        yield return instructionsList[i++];

    }
}