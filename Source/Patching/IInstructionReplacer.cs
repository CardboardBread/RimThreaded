using HarmonyLib;
using System.Collections.Generic;

namespace RimThreaded.Patching
{
    // Simple interface for patching attributes to replace instructions independently, used in combination with a Harmony transpiler.
    public interface IInstructionReplacer
    {
        IEnumerable<CodeInstruction> ApplyPatch(CodeInstruction instruction);
        bool IsPatchTarget(CodeInstruction instruction);
    }
}