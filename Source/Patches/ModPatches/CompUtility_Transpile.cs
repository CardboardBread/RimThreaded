﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace RimThreaded.Patches.ModPatches;

public class CompUtility_Transpile
{
    public static IEnumerable<CodeInstruction> CompGuest(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
    {
        List<CodeInstruction> instructionsList = instructions.ToList();
        Type loadLockObjectType = typeof(Dictionary<,>).MakeGenericType(new[] { typeof(Pawn), Hospitality_Patch.hospitalityCompGuest });
        List<CodeInstruction> loadLockObjectInstructions = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(Hospitality_Patch.hospitalityCompUtility, "guestComps"))
        };
        List<CodeInstruction> searchInstructions = loadLockObjectInstructions.ListFullCopy();
        searchInstructions.Add(new CodeInstruction(OpCodes.Ldarg_0));
        searchInstructions.Add(new CodeInstruction(OpCodes.Ldloc_0));
        searchInstructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(loadLockObjectType, "Add")));

        int i = 0;
        int matchesFound = 0;

        while (i < instructionsList.Count)
        {
            if (RimThreadedHarmony.IsCodeInstructionsMatching(searchInstructions, instructionsList, i))
            {
                matchesFound++;
                foreach (CodeInstruction codeInstruction in RimThreadedHarmony.GetLockCodeInstructions(
                             iLGenerator, instructionsList, i, searchInstructions.Count, loadLockObjectInstructions, loadLockObjectType))
                {
                    yield return codeInstruction;
                }
                i += searchInstructions.Count;
            }
            else
            {
                yield return instructionsList[i];
                i++;
            }
        }
        if (matchesFound < 1)
        {
            Log.Error("IL code instructions not found");
        }
    }
    public static IEnumerable<CodeInstruction> OnPawnRemoved(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
    {
        List<CodeInstruction> instructionsList = instructions.ToList();
        Type loadLockObjectType = typeof(Dictionary<,>).MakeGenericType(new Type[] { typeof(Pawn), Hospitality_Patch.hospitalityCompGuest });
        List<CodeInstruction> loadLockObjectInstructions = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(Hospitality_Patch.hospitalityCompUtility, "guestComps"))
        };
        List<CodeInstruction> searchInstructions = loadLockObjectInstructions.ListFullCopy();
        searchInstructions.Add(new CodeInstruction(OpCodes.Ldarg_0));
        searchInstructions.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(loadLockObjectType, "Remove", new Type[] { typeof(Pawn) })));
        searchInstructions.Add(new CodeInstruction(OpCodes.Pop));

        int i = 0;
        int matchesFound = 0;

        while (i < instructionsList.Count)
        {
            if (RimThreadedHarmony.IsCodeInstructionsMatching(searchInstructions, instructionsList, i))
            {
                matchesFound++;
                foreach (CodeInstruction codeInstruction in RimThreadedHarmony.GetLockCodeInstructions(
                             iLGenerator, instructionsList, i, searchInstructions.Count, loadLockObjectInstructions, loadLockObjectType))
                {
                    yield return codeInstruction;
                }
                i += searchInstructions.Count;
            }
            else
            {
                yield return instructionsList[i];
                i++;
            }
        }
        if (matchesFound < 1)
        {
            Log.Error("IL code instructions not found");
        }
    }
}