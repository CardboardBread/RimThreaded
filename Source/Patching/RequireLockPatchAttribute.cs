using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Reflection;

namespace RimThreaded.Patching;

/// <summary>
/// An attribute to declare a lock should be injected when some member/type/parameter is used.
/// If declared on a patch class and provided with the patch's original class, the attribute will by default add locks
/// on the instance to every instance method. This is through interpreting the default locking behaviour of wrapping
/// instance methods in instance locks, and interpreting being declared on the class as targeting every compatible method.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
public class RequireLockPatchAttribute : SingleTargetPatchAttribute<MethodInfo>
{
    
    
    public RequireLockType LockType { get; set; } = RequireLockType.Instance;
    public int? ParameterIndex { get; set; } = null;
    public Type ParameterType { get; set; } = null;
    public bool WrapMethod { get; set; } = true;
    public bool LockAll { get; set; } = false;

    public override void Locate(MemberInfo parent)
    {
        base.Locate(parent);
    }

    public bool IsLockAll()
    {
        return LockAll || (IsLocated() && Parent.HasHarmonyPatchAll());
    }

    protected override MethodInfo ResolveTarget()
    {
        return AccessTools.DeclaredMethod(DeclaringType, MemberName) ?? (LockAll ? null : throw new ArgumentException(nameof(Parent)));
    }
}

/// <summary>
/// 
/// </summary>
public class RequireLockAllAttribute : SingleTargetPatchAttribute
{
    protected override MemberInfo LocateTarget(MemberInfo member)
    {
        return null;
    }
}

public enum RequireLockType
{
    None,
    Instance,
    Parameter,
    Field
}