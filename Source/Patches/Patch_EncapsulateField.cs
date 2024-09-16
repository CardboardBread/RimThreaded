using System;
using System.Collections.Generic;
using RimThreaded.Patching;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimThreaded.Utilities;
using UnityEngine.Assertions;
using Verse;

namespace RimThreaded.Patches;

// Executing field-to-method conversion in a harmony patch class.
// TODO: track all member replacements that add the constraint of no address usage, highlight possible conflicts in other mods' patches.
[HarmonyPatch, PatchCategory(RimThreadedHarmony.DestructiveCategory)]
public static class Patch_EncapsulateField
{
    static Patch_EncapsulateField()
    {
        RimThreadedMod.GameInstructionScanningEvent += ScanGameInstructions;
        RimThreadedMod.DiscoverLocalAttributeEvent += FindTargetAttribute;
    }

    private static void FindTargetAttribute(object sender, DiscoverAttributeEventArgs eventArgs)
    {
        // Grab all the encapsulate patches in this mod
        foreach (var patchAttribute in eventArgs.Attributes.OfType<EncapsulateFieldPatchAttribute>())
        {
            _attributes.Add(patchAttribute);
            _targetFields.Add(patchAttribute.Target);
            _attributesByField.NewValueIfAbsent(patchAttribute.Target).Add(patchAttribute);
        }
    }

    private static void ScanGameInstructions(object sender, InstructionScanningEventArgs eventArgs)
    {
        var isTargeted = false;
        foreach (var ((opcode, operand), index) in eventArgs.MethodBody.WithIndex())
        {
            if (operand is FieldInfo field && _targetFields.Contains(field))
            {
                if (opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
                {
                    RTLog.Error(
                        $"{nameof(Patch_EncapsulateField)} detected addressing of {field} at: {eventArgs.Method.GetTraceSignature()}:{index}");
                }

                isTargeted = true;
            }
        }

        if (isTargeted) _targetMethods.Add(eventArgs.Method);
    }

    private static HashSet<EncapsulateFieldPatchAttribute> _attributes = new();
    private static HashSet<FieldInfo> _targetFields = new();
    private static Dictionary<FieldInfo, List<EncapsulateFieldPatchAttribute>> _attributesByField = new();
    private static List<MethodBase> _targetMethods = new();

    [HarmonyPrepare]
    internal static bool Prepare(Harmony harmony, MethodBase original = null)
    {
        if (original == null) // Runs before TargetMethods
        {
            RTLog.HarmonyDebugMessage($"Starting {nameof(Patch_EncapsulateField)}");
            return true;
        }
        else // Runs before Execute
        {
            RTLog.HarmonyDebugMessage($"Encapsulating fields on {original}");
            return true;
        }
    }

    [HarmonyTargetMethods]
    internal static IEnumerable<MethodBase> TargetMethods(Harmony harmony) => _targetMethods;

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions,
        ILGenerator iLGenerator,
        MethodBase original)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.operand is FieldInfo addressField &&
                _attributesByField.ContainsKey(addressField) &&
                instruction.IsFieldAddressed())
            {
                throw new Exception("Field encapsulation cannot be performed on a field whose address is used");
                // Ideally there is some workaround to create a field/variable and pass that things address, but we need to somehow
                // determine the lifespan of the backing field, which is difficult to determine.
            }

            if (instruction.operand is FieldInfo multiField &&
                _attributesByField.TryGetValue(multiField, out var targets) &&
                targets.Count(attr => instruction.opcode == attr.MatchingOpcode) > 1)
            {
                throw new AttributeUsageException("Multiple attributes detected encapsulating the same instruction");
            }
            
            if (instruction.operand is FieldInfo field &&
                _attributesByField.TryGetValue(field, out var potentialAttributes) &&
                potentialAttributes.FirstOrDefault(attr => instruction.opcode == attr.MatchingOpcode) is { } patcher)
            {
                Assert.AreEqual(field, patcher.Target);
                yield return patcher.ApplyPatch(instruction);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    [HarmonyCleanup]
    internal static void Cleanup(Harmony harmony, MethodBase original = null, Exception ex = null)
    {
        if (original == null) // Runs when patching is complete
        {
            if (!Prefs.DevMode)
            {
                _attributes.Clear();
                _attributes = null;
                _attributesByField.Clear();
                _attributesByField = null;
                _targetFields.Clear();
                _targetFields = null;
                _targetMethods.Clear();
                _targetMethods = null;
            }

            if (Prefs.DevMode && Prefs.LogVerbose)
            {
            }
        }
    }
}