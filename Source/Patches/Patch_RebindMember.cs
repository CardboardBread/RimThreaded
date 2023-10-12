using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.IO;
using System.Linq;
using System.Reflection.Emit;

namespace RimThreaded.Patches
{
    // Executing static member rebinding in a harmony patch class, to hand over patching control to the local harmony instance.
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch]
    public static class Patch_RebindMember
    {
        private static HashSet<RebindFieldPatchAttribute> rebindFields;
        private static HashSet<FieldInfo> targetFields;
        private static Dictionary<FieldInfo, RebindFieldPatchAttribute> rebindFieldsByField;
        private static HashSet<RebindMethodPatchAttribute> rebindMethods;
        private static Dictionary<MethodInfo, RebindMethodPatchAttribute> rebindMethodsByMethod;

        [HarmonyPrepare]
        public static bool Prepare(Harmony harmony, MethodBase original = null)
        {
            if (harmony is null)
            {
                throw new ArgumentNullException(nameof(harmony));
            }

            if (original == null)
            {
                RTLog.HarmonyDebugMessage($"Starting {nameof(Patch_RebindMember)}");

                rebindFields = new();
                targetFields = new();
                rebindFieldsByField = new();
                rebindMethods = new();
                rebindMethodsByMethod = new();

                foreach (var rebind in RimThreadedMod.Instance.GetLocalAttributesByType<RebindFieldPatchAttribute>())
                {
                    rebindFields.Add(rebind);
                    targetFields.Add(rebind.Target);
                    rebindFieldsByField[rebind.Target] = rebind;
                }

                foreach (var replace in RimThreadedMod.Instance.GetLocalAttributesByType<RebindMethodPatchAttribute>())
                {
                    rebindMethods.Add(replace);
                    rebindMethodsByMethod[replace.Target] = replace;
                }

                return true;
            }
            else
            {
                RTLog.HarmonyDebugMessage($"Rebinding members in {original}");
                return true;
            }
        }

        // Target every method, constructor, getter and setter that uses any targeted member.
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
                        RTLog.Error($"{nameof(Patch_RebindMember)} detected addressing of {field} at: {method.GetTraceSignature()}:{index}");
                    }
                    yield return index;
                }
            }
        }

        // TODO: if the replacement field is the same type of the original, just replace operands accessing the field. if the
        //       replacement is a ThreadLocal<T> where T is the original field's type, replace the store/load with proper calls
        //       and raise an error if the field's address is ever required.
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
                    rebindFieldsByField.TryGetValue(field, out var fRebind) &&
                    fRebind.IsPatchTarget(instruction))
                {
                    // TODO: log replacing instruction with rebind
                    foreach (var insert in fRebind.ApplyPatch(instruction))
                    {
                        yield return insert;
                    }
                }
                else if (instruction.operand is MethodInfo method &&
                    rebindMethodsByMethod.TryGetValue(method, out var mRebind) &&
                    mRebind.IsPatchTarget(instruction))
                {
                    foreach (var insert in mRebind.ApplyPatch(instruction))
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
            if (harmony is null)
            {
                throw new ArgumentNullException(nameof(harmony));
            }

            if (original == null && !Prefs.DevMode)
            {
                rebindFields.Clear();
                rebindFields = null;
                targetFields.Clear();
                targetFields = null;
                rebindFieldsByField.Clear();
                rebindFieldsByField = null;
                rebindMethods.Clear();
                rebindMethods = null;
                rebindMethodsByMethod.Clear();
                rebindMethodsByMethod = null;
            }
        }
    }
}
