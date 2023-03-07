using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Utilities
{
    // An attribute to declare a lock should be injected when some member/type/parameter is used.
    // Declared on a patch class and provided with the patch's original class, the attribute will by default add locks
    // on the instance to every instance method.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
    public class RequireLockAttribute : SingleTargetPatchAttribute
    {
        public static IEnumerable<RequireLockAttribute> GetLocalUsages() => AttributeUtility.GetLocalUsages<RequireLockAttribute>();

        internal const int invalidIndex = -1;

        public RequireLockType LockType { get; set; } = RequireLockType.Instance;
        public int ParameterIndex { get; set; } = invalidIndex;
        public Type ParameterType { get; set; } = null;
        public bool WrapMethod { get; set; } = true;

        internal new MethodInfo _target;

        public RequireLockAttribute()
        {
        }

        public RequireLockAttribute(string memberName)
        {
            MemberName = memberName;
        }

        public RequireLockAttribute(Type declaringType, string memberName) : this(memberName)
        {
            DeclaringType = declaringType;
        }

        internal override void Locate(HarmonyMethod nearby)
        {
            _target ??= AccessTools.DeclaredMethod(DeclaringType, MemberName);
        }
    }
}
