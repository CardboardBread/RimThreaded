using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RimThreaded.Patches
{
    // Harmony patch class for replacing direct field accesses with thread-safe accesses.
    // Should use System.Threading.Volatile and System.Threading.Interlocked
    [HarmonyPatch]
    public static class Patch_Volatile
    {
        private static HashSet<FieldInfo> targetFields = new();

        public static void Prepare(Harmony harmony, MethodBase original)
        {
            // TODO: find all fields that are marked as requiring intervention.
        }

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            // TODO: search in game assemblies for methods containing references to any target fields
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions,
                                                           ILGenerator iLGenerator,
                                                           MethodBase original)
        {
            // TODO: Replace direct field accesses with System.Threading.Volatile calls
            // TODO: Raise errors when the aaddress of any target field is used
        }

        [HarmonyCleanup]
        public static void Cleanup(Harmony harmony, MethodBase original, Exception ex)
        {
            // TODO: If not in some kind of debug mode, dispose as much data as possible
        }
    }
}
