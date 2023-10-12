using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Assertions;
using static UnityEngine.GraphicsBuffer;

namespace RimThreaded.Patching
{
    // Harmony patch-style declaration for replacing a field with a getter and/or setter method.
    // Instance fields require a getter that will take the instance as an argument, and a setter that takes the instance
    // and the new value as arguments.
    // TODO: allow encapsulating with assignable types, such that the encapsulator could return a subtype.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class EncapsulateFieldPatchAttribute : SingleTargetPatchAttribute<MethodInfo, FieldInfo>, ILocationAware, IInstructionReplacer
    {
        public delegate FieldType InstanceLoad<EnclosingType, FieldType>(EnclosingType enclosing);
        public delegate FieldType StaticLoad<FieldType>();
        public delegate void InstanceStore<EnclosingType, FieldType>(EnclosingType enclosing, FieldType value);
        public delegate void StaticStore<FieldType>(FieldType value);

        public EncapsulateFieldType? PatchType { get; set; } = null;

        public EncapsulateFieldPatchAttribute()
        {
        }

        public EncapsulateFieldPatchAttribute(Type declaringType)
        {
            DeclaringType = declaringType;
        }

        public EncapsulateFieldPatchAttribute(string memberName)
        {
            MemberName = memberName;
        }

        public EncapsulateFieldPatchAttribute(Type declaringType, string memberName)
        {
            DeclaringType = declaringType;
            MemberName = memberName;
        }

        internal override MemberInfo ResolveTarget()
        {
            if (ResolveDeclaringType() is Type declaring && ResolveMemberName() is string member)
            {
                return AccessTools.DeclaredField(declaring, member) ?? throw new ArgumentException(nameof(Target));
            }
            return null;
        }

        public override void Locate(MemberInfo member)
        {
            base.Locate(member);

            // infer the patch type, or verify the user selected the right one.
            var inferred = GetPatchType();
            if (PatchType != null && PatchType != inferred)
            {
                throw new Exception();
            }
            else
            {
                PatchType ??= inferred;
            }
        }

        private EncapsulateFieldType GetPatchType()
        {
            if (IsStaticLoad())
            {
                return EncapsulateFieldType.StaticLoad;
            }
            else if (IsStaticStore())
            {
                return EncapsulateFieldType.StaticStore;
            }
            else if (IsInstanceLoad())
            {
                return EncapsulateFieldType.InstanceLoad;
            }
            else if (IsInstanceStore())
            {
                return EncapsulateFieldType.InstanceStore;
            }
            else
            {
                throw new ArgumentException($"Could not infer patch type for {this}");
            }
        }

        private EncapsulateFieldType GetPatchType(CodeInstruction instruction)
        {

        }

        private bool IsInstanceStore()
        {
            return !Target.IsStatic
                && Parent.ReturnType == typeof(void)
                && Parent.GetParameters().Length == 2
                && Parent.GetParameters()[0].ParameterType == Target.DeclaringType
                && Parent.GetParameters()[1].ParameterType == Target.FieldType;
        }

        private bool IsInstanceStore(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Stfld
                && PatchType == EncapsulateFieldType.InstanceStore;
        }

        private bool IsInstanceLoad()
        {
            return !Target.IsStatic
                && Parent.ReturnType == Target.FieldType
                && Parent.GetParameters().Length == 1
                && Parent.GetParameters()[0].ParameterType == Target.DeclaringType;
        }

        private bool IsInstanceLoad(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldfld
                && PatchType == EncapsulateFieldType.InstanceLoad;
        }

        private bool IsStaticStore()
        {
            return Target.IsStatic
                && Parent.ReturnType == typeof(void)
                && Parent.GetParameters().Length == 1
                && Parent.GetParameters()[0].ParameterType == Target.FieldType;
        }

        private bool IsStaticStore(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Stsfld
                && PatchType == EncapsulateFieldType.StaticStore;
        }

        private bool IsStaticLoad()
        {
            return Target.IsStatic
                && Parent.ReturnType == Target.FieldType
                && Parent.GetParameters().Length == 0;
        }

        private bool IsStaticLoad(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldsfld
                && PatchType == EncapsulateFieldType.StaticLoad;
        }

        public bool IsPatchTarget(CodeInstruction instruction)
        {
            // Ensure instruction is target before further verification
            if (instruction.operand is FieldInfo original && original == Target)
            {
                if (instruction.opcode == OpCodes.Ldflda || instruction.opcode == OpCodes.Ldsflda)
                {
                    throw new ArgumentException("Field encapsulation cannot be performed on a field whos address is used.");
                    // Ideally there is some workaround to create a field/variable and pass that things address, but we need to somehow
                    // determine the lifespan of the backing field, which is difficult to determine.
                }

                return PatchType switch
                {
                    EncapsulateFieldType.StaticStore => IsStaticStore(instruction),
                    EncapsulateFieldType.StaticLoad => IsStaticLoad(instruction),
                    EncapsulateFieldType.InstanceStore => IsInstanceStore(instruction),
                    EncapsulateFieldType.InstanceLoad => IsInstanceLoad(instruction),
                    EncapsulateFieldType.None => false,
                    _ => throw new NotImplementedException(),
                };
            }
            return false;
        }

        public IEnumerable<CodeInstruction> ApplyPatch(CodeInstruction instruction)
        {
            instruction.opcode = OpCodes.Call;
            instruction.operand = Parent;
            yield return instruction;
        }

        protected override MemberInfo LocateTarget(MemberInfo member)
        {
            return AccessTools.DeclaredField(DeclaringType, MemberName) ?? throw new ArgumentException(nameof(member));
        }
    }

    [Flags]
    public enum EncapsulateFieldType
    {
        InstanceLoad,
        InstanceStore,
        StaticLoad,
        StaticStore,
        //Both = Load | Store
    }
}
