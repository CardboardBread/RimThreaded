using System;
using System.Collections.Generic;
using System.Reflection;
using RimThreaded.Utilities;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using UnityEngine.Assertions;

namespace RimThreaded.Patching;

// A non-Harmony patch attribute to declare a field should be replaced with a dynamically created or referenced premade
// version.
//
// The replacement process will be enacted through redirecting all references from the old field to the new.
// Operates independent of location, but can grab info from nearby harmony attributes (same member or declaring type).
//
// Considered a 'rebind' not a 'replace' patch since it works by changing references to the target, instead of modifying
// the target.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
public class RebindFieldPatchAttribute : SingleTargetPatchAttribute<FieldInfo>, ILocationAware
{
    public bool SelfInitialized { get; set; } = false;
    public Type InitializerType { get; set; } = null;
    public string InitializerName { get; set; } = null;
    public RebindFieldType PatchType { get; set; } = RebindFieldType.None;

    public RebindFieldPatchAttribute()
    {
    }

    public RebindFieldPatchAttribute(Type declaringType) : base(declaringType)
    {
    }

    public RebindFieldPatchAttribute(string memberName) : base(memberName)
    {
    }

    public RebindFieldPatchAttribute(Type declaringType, string memberName) : base(declaringType, memberName)
    {
    }

    public override void Locate(MemberInfo member)
    {
        base.Locate(member);
        Assert.IsTrue(Target.IsStatic);

        switch (PatchType)
        {
            case RebindFieldType.None:
                break; // infer patch type
            case RebindFieldType.Premade:
                Assert.IsTrue();
        }

        if (!Target.IsStatic || (Parent is FieldInfo fParent && !fParent.IsStatic))
        {
            throw new ArgumentException("Field rebinding cannot be done on instance fields");
        }

        if (Parent is FieldInfo premade && premade.FieldType != Target.FieldType && premade.FieldType != typeof(ThreadLocal<>).MakeGenericType(Target.FieldType))
        {
            throw new ArgumentException($"Premade field rebinding must be done with equivalent field types or the usage type boxing the target type in {typeof(ThreadLocal<>)}");
        }

        bool IsThreadLocal()
        {
            return Parent is FieldInfo premade && premade.FieldType == typeof(ThreadLocal<>).MakeGenericType(Target.FieldType);
        }
    }

    protected override MemberInfo LocateTarget(MemberInfo member)
    {
        return AccessTools.DeclaredField(DeclaringType, MemberName) ?? throw new ArgumentException(nameof(member));
    }

    public bool IsPatchTarget(CodeInstruction instruction)
    {
        Assert.IsTrue(IsLocated());

        if (instruction.operand is FieldInfo original && original == Target)
        {
            if (instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Stfld || instruction.opcode == OpCodes.Ldflda)
            {
                throw new ArgumentException($"Instance field instructions were detected referencing {Target}, dynamic rebind is incompatible");
            }

            return true;
        }
        return false;
    }

    public IEnumerable<CodeInstruction> ApplyPatch(CodeInstruction instruction)
    {
        Assert.IsTrue(IsLocated());

        if (Parent is not FieldInfo)
        {
            var newType = RimThreadedHarmony.GetReplacingType(Target.DeclaringType);
            var newField = newType.DefineField(Target.Name,
                Target.FieldType,
                Target.Attributes | FieldAttributes.Static);
            instruction.operand = newField;
            yield return instruction;
        }
        else if (IsThreadLocal())
        {
            if (instruction.opcode == OpCodes.Ldflda || instruction.opcode == OpCodes.Ldsflda)
            {
                throw new ArgumentException("ThreadLocal rebinding cannot be performed on a field whos address is used.");
            }

            if (instruction.opcode == OpCodes.Ldsfld)
            {

            }

            if (instruction.opcode == OpCodes.Stsfld)
            {

            }
        }
        else if (Parent is FieldInfo premade)
        {
            instruction.operand = premade;
            yield return instruction;
        }
    }

    public override MemberInfo ResolveTarget()
    {
        throw new NotImplementedException();
    }
}

public enum RebindFieldType
{
    None,
    Premade,
    ThreadLocalPremade,
    Dynamic,
}