using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.Linq;
using System.Reflection.Emit;

namespace RimThreaded.Patches
{
    // Executing field-to-method conversion in a harmony patch class.
    // TODO: track all member replacements that add the constraint of no address usage, highlight possible conflicts in other mods' patches.
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch]
    public static class Patch_EncapsulateField
    {
        private static HashSet<EncapsulateFieldPatchAttribute> attributes;
        private static HashSet<FieldInfo> targetFields;
        private static Dictionary<FieldInfo, EncapsulateFieldPatchAttribute> attributesByField;

        [HarmonyPrepare]
        public static bool Prepare(Harmony harmony, MethodBase original = null)
        {
            if (harmony is null)
            {
                throw new ArgumentNullException(nameof(harmony));
            }

            if (original == null)
            {
                RTLog.HarmonyDebugMessage($"Starting {nameof(Patch_EncapsulateField)}");

                attributes = new();
                targetFields = new();
                attributesByField = new();

                // Grab all the encapsulate patches in this mod
                foreach (var attribute in RimThreadedMod.Instance.GetLocalAttributesByType<EncapsulateFieldPatchAttribute>())
                {
                    attributes.Add(attribute);
                    targetFields.Add(attribute.Target);
                    attributesByField[attribute.Target] = attribute;
                }

                return true;
            }
            else
            {
                RTLog.HarmonyDebugMessage($"Encapsulating fields on {original}");
                return true;
            }
        }

        // Search for every method, constructor, getter and setter that references fields we are encapsulating.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            if (harmony is null)
            {
                throw new ArgumentNullException(nameof(harmony));
            }

            foreach (var target in from assembly in RimThreaded.GameAssemblies().AsParallel()
                                   from methodBase in assembly.AllMethodBases()
                                   where FindAllTargetedInstructions(methodBase).Count() > 0
                                   select methodBase)
            {
                yield return target;
            }
        }

        private static IEnumerable<int> FindAllTargetedInstructions(MethodBase method, HarmonyMethodBody methodBody = null)
        {
            methodBody ??= RimThreadedHarmony.ReadMethodBody(method);
            foreach (var ((opcode, operand), index) in methodBody.WithIndex())
            {
                if (operand is FieldInfo field && targetFields.Contains(field))
                {
                    if (opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
                    {
                        RTLog.Error($"{nameof(Patch_EncapsulateField)} detected addressing of {field} at: {method.GetTraceSignature()}:{index}");
                    }
                    yield return index;
                }
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions,
                                                           ILGenerator iLGenerator,
                                                           MethodBase original)
        {
            if (instructions is null)
            {
                throw new ArgumentNullException(nameof(instructions));
            }
            if (iLGenerator is null)
            {
                throw new ArgumentNullException(nameof(iLGenerator));
            }
            if (original is null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            foreach (var instruction in instructions)
            {
                if (instruction.operand is FieldInfo field &&
                    attributesByField.TryGetValue(field, out var encapsulate) &&
                    encapsulate.IsPatchTarget(instruction))
                {
                    foreach (var insert in encapsulate.ApplyPatch(instruction))
                    {
                        yield return insert;
                    }
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        [HarmonyCleanup]
        public static void Cleanup(Harmony harmony, MethodBase original = null, Exception ex = null)
        {
            if (original == null)
            {
                if (!Prefs.DevMode)
                {
                    attributes.Clear();
                    attributes = null;
                    attributesByField.Clear();
                    attributesByField = null;
                    targetFields.Clear();
                    targetFields = null;
                }

                if (Prefs.DevMode && Prefs.LogVerbose)
                {
                    
                }
            }
        }
    }
}
