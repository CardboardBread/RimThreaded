using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Assertions;
using static UnityEngine.GraphicsBuffer;

namespace RimThreaded.Patching;

/// <summary>
/// Harmony patch-style declaration for replacing a field with a getter and/or setter method.
/// Instance fields require a getter that will take the instance as an argument, and a setter that takes the instance
/// and the new value as arguments.
/// </summary>
// TODO: allow encapsulating with assignable types, such that the encapsulator could return a subtype.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class EncapsulateFieldPatchAttribute : SingleTargetPatchAttribute<MethodInfo, FieldInfo>
{
    public delegate TField InstanceLoad<in TEnclosing, out TField>(TEnclosing enclosing);
    public delegate TField StaticLoad<out TField>();
    public delegate void InstanceStore<in TEnclosing, in TField>(TEnclosing enclosing, TField value);
    public delegate void StaticStore<in TField>(TField value);
    
    public EncapsulateFieldType? PatchType { get; set; }

    protected override FieldInfo ResolveTarget() => AccessTools.DeclaredField(DeclaringType, MemberName);

    public override void Locate(MemberInfo member)
    {
        base.Locate(member);

        // verify the attribute's declaration matches the selected patch type
        if (PatchType == EncapsulateFieldType.StaticLoad && !IsStaticLoad()) throw new AttributeUsageException();
        if (PatchType == EncapsulateFieldType.StaticStore && !IsStaticStore()) throw new AttributeUsageException();
        if (PatchType == EncapsulateFieldType.InstanceLoad && !IsInstanceLoad()) throw new AttributeUsageException();
        if (PatchType == EncapsulateFieldType.InstanceStore && !IsInstanceStore()) throw new AttributeUsageException();
    }

    public OpCode MatchingOpcode =>
        PatchType switch
        {
            EncapsulateFieldType.InstanceLoad => OpCodes.Ldfld,
            EncapsulateFieldType.InstanceStore => OpCodes.Stfld,
            EncapsulateFieldType.StaticLoad => OpCodes.Ldsfld,
            EncapsulateFieldType.StaticStore => OpCodes.Stsfld,
            _ => throw new ArgumentOutOfRangeException()
        };

    /// <summary>
    /// Verifies if the parent member of this attribute matches the type signature of an instance store instruction.
    /// </summary>
    private bool IsInstanceStore() =>
        !Target.IsStatic
        && Parent.ReturnType == typeof(void)
        && Parent.GetParameters().Length == 2
        && Parent.GetParameters()[0].ParameterType == Target.DeclaringType
        && Parent.GetParameters()[1].ParameterType == Target.FieldType;

    /// <summary>
    /// Verifies if the parent member of this attribute matches the type signature of an instance load instruction. 
    /// </summary>
    private bool IsInstanceLoad() =>
        !Target.IsStatic
        && Parent.ReturnType == Target.FieldType
        && Parent.GetParameters().Length == 1
        && Parent.GetParameters()[0].ParameterType == Target.DeclaringType;

    /// <summary>
    /// Verifies if the parent member of this attribute matches the type signature of a static store instruction.
    /// </summary>
    private bool IsStaticStore() =>
        Target.IsStatic
        && Parent.ReturnType == typeof(void)
        && Parent.GetParameters().Length == 1
        && Parent.GetParameters()[0].ParameterType == Target.FieldType;

    /// <summary>
    /// Verifies if the parent member of this attribute matches the type signature of a static load instruction. 
    /// </summary>
    private bool IsStaticLoad() =>
        Target.IsStatic
        && Parent.ReturnType == Target.FieldType
        && Parent.GetParameters().Length == 0;

    private bool IsStaticLoad(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldsfld
               && PatchType == EncapsulateFieldType.StaticLoad;
    }

    public CodeInstruction ApplyPatch(CodeInstruction instruction)
    {
        instruction.opcode = OpCodes.Call;
        instruction.operand = Parent;
        return instruction;
    }
}

public enum EncapsulateFieldType
{
    None,
    InstanceLoad,
    InstanceStore,
    StaticLoad,
    StaticStore,
}