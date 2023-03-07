using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    // Harmony patch-style declaration for replacing a field with a getter and/or setter method.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class FieldPatchAttribute : SingleTargetPatchAttribute
    {
        public static IEnumerable<FieldPatchAttribute> GetLocalUsages() => AttributeUtility.GetLocalUsages<FieldPatchAttribute>();

        public FieldPatchType PatchType { get; set; } = FieldPatchType.None;

        internal new FieldInfo _target;
        internal MethodInfo _method;

        public FieldPatchAttribute()
        {
        }

        public FieldPatchAttribute(string memberName)
        {
            MemberName = memberName;
        }

        public FieldPatchAttribute(Type declaringType, string memberName)
        {
            DeclaringType = declaringType;
            MemberName = memberName;
        }

        internal override void Locate(HarmonyMethod nearby)
        {
            _target ??= AccessTools.DeclaredField(DeclaringType, MemberName);
            _method = (MethodInfo)_parent;
        }

        public bool IsTarget(CodeInstruction instruction)
        {
            return PatchType switch
            {
                FieldPatchType.None => false,
                FieldPatchType.Load => (instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Ldsfld)
                                        && instruction.operand is FieldInfo load
                                        && load == _target,
                FieldPatchType.Store => (instruction.opcode == OpCodes.Stfld || instruction.opcode == OpCodes.Stsfld)
                                        && instruction.operand is FieldInfo store
                                        && store == _target,
                FieldPatchType.Both => instruction.operand is FieldInfo both
                                        && both == _target,
                _ => throw new NotImplementedException(),
            };
        }

        public void ApplyPatch(CodeInstruction instruction)
        {
            instruction.opcode = OpCodes.Call;
            instruction.operand = _method;
        }
    }
}
