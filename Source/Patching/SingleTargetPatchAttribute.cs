using RimThreaded.Utilities;
using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace RimThreaded.Patching;

/// <summary>
/// Base pattern for harmony-style attributes that target some named member, and perform a patch between the named member and the attribute's declaring member.
/// </summary>
public abstract class SingleTargetPatchAttribute<TParent, TTarget> : HarmonyPatch, ILocationAware where TParent : MemberInfo where TTarget : MemberInfo
{
    private bool _isLocated;
    
    public Type DeclaringType { get => info.declaringType ?? Parent.HarmonyDeclaringType(); set => info.declaringType = value; }
    public string MemberName { get => info.methodName ?? Parent.HarmonyMethodName() ?? Parent.Name; set => info.methodName = value; }
    public MethodType? MethodType { get => info.methodType ?? Parent.HarmonyMethodType(); set => info.methodType = value; }
    public Type[] ArgumentTypes { get => info.argumentTypes ?? Parent.HarmonyArgumentTypes(); set => info.argumentTypes = value; }

    /// <summary>
    /// The member this attribute is declared on.
    /// </summary>
    public TParent Parent { get; protected set; }

    /// <summary>
    /// The member this patch will target and/or manipulate.
    /// </summary>
    public TTarget Target { get; protected set; }

    protected abstract TTarget ResolveTarget();

    public bool IsLocated() => _isLocated;
    internal bool HasTarget() => IsLocated() && Target is not null;
    internal bool IsTargetMissing() => IsTargetReferenced() && Target is null;
    internal bool IsTargetReferenced() => DeclaringType is not null && MemberName is not null;

    public virtual void Locate(MemberInfo parent)
    {
        Parent = parent as TParent ?? throw new ArgumentException("Parent member is of incorrect type", nameof(parent));
        Target = ResolveTarget() ?? throw new ArgumentNullException(nameof(Target));
        _isLocated = true;
    }
}

public abstract class SingleTargetPatchAttribute<T> : SingleTargetPatchAttribute<MemberInfo, T> where T : MemberInfo
{
}

public abstract class SingleTargetPatchAttribute : SingleTargetPatchAttribute<MemberInfo, MemberInfo>
{
}