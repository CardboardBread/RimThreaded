using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patching
{
    // Data class to model a sequence of instructions to find through harmony-transpiling a method.
    [Obsolete]
    public class InstructionPattern
    {
        public readonly List<InstructionReplacement> pattern;

        public InstructionPattern(params InstructionReplacement[] pattern)
        {
            this.pattern = pattern.ToList();
        }

        public InstructionPattern(IEnumerable<InstructionReplacement> pattern)
        {
            this.pattern = pattern.ToList();
        }

        // The number of consecutive instructions in the pattern.
        public int SliceLength => pattern.Count();

        public IEnumerable<int> Matches(IEnumerable<CodeInstruction> instructions)
        {
            for (int depth = 0; depth < instructions.Count() - pattern.Count(); depth++)
            {
                var slice = GetSlice(instructions, depth);
                if (SliceMatches(slice))
                {
                    yield return depth;
                }
            }
        }

        private IEnumerable<CodeInstruction> GetSlice(IEnumerable<CodeInstruction> instructions, int depth)
        {
            return instructions.Skip(depth).Take(SliceLength);
        }

        private bool SliceMatches(IEnumerable<CodeInstruction> slice)
        {
            var patternStep = pattern.GetEnumerator();
            var sliceStep = slice.GetEnumerator();
            while (patternStep.MoveNext() && sliceStep.MoveNext())
            {
                if (!patternStep.Current.Matches(sliceStep.Current))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
