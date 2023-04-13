using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace RimThreaded.Patching
{
    // Data class to model replacing all instructions matching the provided objects/predicates.
    // The fields used for matching may be left null as an ignore case. e.g. match on every `OpCodes.Callvirt` instruction.
    [Obsolete]
    public class InstructionReplacement
    {
        // matching objects, leaving them null means skipping/ignoring.
        public readonly OpCode? opCode;
        public readonly object operand;
        public readonly IEnumerable<Label> labels;
        public readonly CodeInstruction instruction;
        public readonly Func<OpCode, bool> opCodePredicate;
        public readonly Func<object, bool> operandPredicate;
        public readonly Func<IEnumerable<Label>, bool> labelsPredicate;
        public readonly Func<CodeInstruction, bool> instructionPredicate;

        public InstructionReplacement(OpCode? opCode = null,
                                      object operand = null,
                                      IEnumerable<Label> labels = null,
                                      CodeInstruction instruction = null,
                                      Func<OpCode, bool> opCodePredicate = null,
                                      Func<object, bool> operandPredicate = null,
                                      Func<IEnumerable<Label>, bool> labelsPredicate = null,
                                      Func<CodeInstruction, bool> instructionPredicate = null)
        {
            this.opCode = opCode;
            this.operand = operand;
            this.labels = labels;
            this.instruction = instruction;

            this.opCodePredicate = opCodePredicate;
            this.operandPredicate = operandPredicate;
            this.labelsPredicate = labelsPredicate;
            this.instructionPredicate = instructionPredicate;
        }

        public bool Matches(CodeInstruction instruction)
        {
            var results = GetMatchResults(instruction);
            if (results.EnumerableNullOrEmpty()) return false; // No matches is a failure.
            return results.All(res => !res.HasValue || res.HasValue && res.Value); // Every result must be ignore or pass.
        }

        // Check if object matches, or predicate if no object, or null(ignore) if neither.
        private IEnumerable<bool?> GetMatchResults(CodeInstruction instruction)
        {
            yield return MatchesObj(instruction);
            yield return MatchesDelegate(instruction);

            yield return MatchesObj(instruction.opcode);
            yield return MatchesDelegate(instruction.opcode);

            yield return MatchesObj(instruction.operand);
            yield return MatchesDelegate(instruction.operand);

            yield return MatchesObj(instruction.labels);
            yield return MatchesDelegate(instruction.labels);
        }

        private bool? MatchesObj(CodeInstruction instruction) => this.instruction != null ? _MatchesObj(instruction) : null;
        private bool _MatchesObj(CodeInstruction instruction)
            => this.instruction.opcode == instruction.opcode
            && this.instruction.operand == instruction.operand
            && this.instruction.labels.SequenceEqual(instruction.labels);
        private bool? MatchesDelegate(CodeInstruction instruction) => instructionPredicate != null ? instructionPredicate.Invoke(instruction) : null;

        private bool? MatchesObj(OpCode opCode) => this.opCode != null ? this.opCode.Value == opCode : null;
        private bool? MatchesDelegate(OpCode opCode) => opCodePredicate != null ? opCodePredicate.Invoke(opCode) : null;

        private bool? MatchesObj(object operand) => this.operand != null ? this.operand == operand : null;
        private bool? MatchesDelegate(object operand) => operandPredicate != null ? operandPredicate.Invoke(operand) : null;

        private bool? MatchesObj(IEnumerable<Label> labels) => this.labels != null ? this.labels.SequenceEqual(labels) : null; // TODO: null check labels field or assume `empty == null`?
        private bool? MatchesDelegate(IEnumerable<Label> labels) => labelsPredicate != null ? labelsPredicate.Invoke(labels) : null;
    }
}
