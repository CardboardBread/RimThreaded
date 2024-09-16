using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RimThreaded.Patching;

// Non-harmony patch attribute for declaring patch methods that will wholly replace existing methods.
// Functions through the same mechanism as `ReplaceFieldPatchAttribute`, and is therefore highly incompatible
// with Harmony patches.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
public class RebindMethodPatchAttribute : SingleTargetPatchAttribute<MethodInfo>, ILocationAware
{
    public RebindMethodPatchAttribute()
    {
    }

    public RebindMethodPatchAttribute(Type targetType, string targetName)
    {
        TargetType = targetType;
        TargetName = targetName;
    }

    public RebindMethodPatchAttribute(Type sourceType, string sourceName, Type targetType, string targetName) : this(targetType, targetName)
    {
        SourceType = sourceType;
        SourceName = sourceName;
    }

    public override void Locate(MemberInfo member)
    {
        base.Locate(member);

        var sourceParamTypes = Source.GetParameters().Select(p => p.ParameterType);
        var targetParamTypes = Target.GetParameters().Select(p => p.ParameterType);
        if (!sourceParamTypes.SequenceEqual(targetParamTypes))
        {
            throw new ArgumentException("Target and source methods have differing parameter signatures");
        }

        if (Source.ReturnType != Target.ReturnType)
        {
            throw new ArgumentException("Target return type does not match source");
        }
    }

    protected override MemberInfo LocateSource(MemberInfo member)
    {
        return AccessTools.DeclaredMethod(SourceType, SourceName) ?? throw new ArgumentException(nameof(member));
    }

    protected override MemberInfo LocateTarget(MemberInfo member)
    {
        return AccessTools.DeclaredMethod(TargetType, TargetName) ?? throw new ArgumentException(nameof(member));
    }
}