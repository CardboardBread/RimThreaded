using System.Collections.Generic;
using Verse.AI;
using HarmonyLib;
using static HarmonyLib.AccessTools;
using System.Reflection.Emit;
using System.Reflection;

namespace RimThreaded.Patches.VerseAIPatches
{
    [HarmonyPatch]
    public static class PathFinder_RegionCostCalculator_Patch
    {
        static FieldInfo field;
        static MethodInfo getter;
        static MethodInfo setter;

        public static void Prepare()
        {
            field = Field(typeof(PathFinder), nameof(PathFinder.regionCostCalculator));
            getter = Method(typeof(PathFinder_Patch), nameof(GetRegionCostCalculator));
            setter = Method(typeof(PathFinder_Patch), nameof(SetRegionCostCalculator));
        }

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            return null; // return every single method in Assembly-CSharp
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RegionCostCalculator(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator, MethodBase original)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo load && load == field)
                {
                    yield return new CodeInstruction(OpCodes.Call, getter);
                }
                else if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo store && store == field)
                {
                    yield return new CodeInstruction(OpCodes.Call, setter);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}
