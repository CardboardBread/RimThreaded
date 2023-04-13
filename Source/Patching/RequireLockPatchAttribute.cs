using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Reflection;

namespace RimThreaded.Patching
{
    // An attribute to declare a lock should be injected when some member/type/parameter is used.
    //
    // If declared on a patch class and provided with the patch's original class, the attribute will by default add locks
    // on the instance to every instance method. This is through interpreting the default locking behaviour of wrapping
    // instance methods in instance locks, and interpreting being declared on the class as targeting every compatible method.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
    public class RequireLockPatchAttribute : SingleTargetPatchAttribute<MethodInfo>, ILocationAware
    {
        public RequireLockPatchAttribute()
        {
        }

        public RequireLockPatchAttribute(Type declaringType) : base(declaringType)
        {
        }

        public RequireLockPatchAttribute(string memberName) : base(memberName)
        {
        }

        public RequireLockPatchAttribute(Type declaringType, string memberName) : base(declaringType, memberName)
        {
        }

        public RequireLockType LockType { get; set; } = RequireLockType.Instance;
        public int? ParameterIndex { get; set; } = null;
        public Type ParameterType { get; set; } = null;
        public bool WrapMethod { get; set; } = true;
        public bool LockAll { get; set; } = false;

        public bool IsLockAll()
        {
            return LockAll || (IsLocated() && Parent.HasHarmonyPatchAll());
        }

        internal override MemberInfo ResolveTarget()
        {
            return AccessTools.DeclaredMethod(DeclaringType, MemberName) ?? (LockAll ? null : throw new ArgumentException(nameof(Parent)));
        }
    }

    // A 
    public class RequireLockAllAttribute : SingleTargetPatchAttribute
    {
        public RequireLockAllAttribute()
        {
        }

        public RequireLockAllAttribute(string memberName) : base(memberName)
        {
        }

        public RequireLockAllAttribute(Type declaringType, string memberName) : base(declaringType, memberName)
        {
        }

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
}
