using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.StaticReplacement
{
    // Declaring static replacements in a harmony patch, to hand over patching control to the local harmony instance.
    [PatchCategory("Destructive")]
    public static class Patch_StaticReplacement
    {
        internal static StaticReplacementAssembly ReplacementAssembly;

        // Target every single method.
        [PatchCategory("Destructive")]
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            return from assembly in AccessTools.AllAssemblies()
                   from type in AccessTools.GetTypesFromAssembly(assembly)
                   from method in AccessTools.GetDeclaredMethods(type)
                   select method;
        }

        [PatchCategory("Destructive")]
        [HarmonyPrepare]
        public static bool Prepare(MethodBase original, Harmony harmony)
        {
            if (original == null)
            {
                ReplacementAssembly = new("RimThreadedReplacements");
                foreach (var request in ReplaceFieldAttribute.GetLocalUsages())
                {
                    ReplacementAssembly.Replace(request._original, request._replacement);
                }
            }

            if (original != null)
            {

            }

            return true;
        }

        [PatchCategory("Destructive")]
        public static void Execute()
        {
        }

        [PatchCategory("Destructive")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator, MethodBase original)
        {
            var enumerator = instructions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;

                ReplacementAssembly.TryDirectReplacement(instruction);

                // Static method calls
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo staticCall)
                {
                    ReplacementAssembly.TryReplaceStaticCall(ref instruction, staticCall);
                }

                // Instance method calls
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo instanceCall)
                {
                    ReplacementAssembly.TryReplaceInstanceCall(ref instruction, instanceCall);
                }

                // Instance fields
                else if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo instanceLoad)
                {
                    ReplacementAssembly.TryReplaceInstanceFieldLoad(ref instruction, instanceLoad);
                }
                else if (instruction.opcode == OpCodes.Ldflda && instruction.operand is FieldInfo instanceAddress)
                {
                    ReplacementAssembly.TryReplaceInstanceFieldAddress(ref instruction, instanceAddress);
                }

                // Static fields
                else if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo staticLoad)
                {
                    ReplacementAssembly.TryReplaceStaticFieldLoad(ref instruction, staticLoad);
                }
                else if (instruction.opcode == OpCodes.Ldsflda && instruction.operand is FieldInfo staticAddress)
                {
                    ReplacementAssembly.TryReplaceStaticFieldAddress(ref instruction, staticAddress);
                }

                // Instance fields
                else if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo instanceStore)
                {
                    ReplacementAssembly.TryReplaceInstanceFieldStore(ref instruction, instanceStore);
                }

                // Static fields
                else if (instruction.opcode == OpCodes.Stsfld && instruction.operand is FieldInfo staticStore)
                {
                    ReplacementAssembly.TryReplaceStaticFieldStore(ref instruction, staticStore);
                }

                yield return instruction;
            }
        }

        [PatchCategory("Destructive")]
        [HarmonyCleanup]
        public static Exception Cleanup(MethodBase original, Harmony harmony, Exception ex)
        {
            return ex;
        }
    }
}
