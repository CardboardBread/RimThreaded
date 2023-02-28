using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    // An attribute to declare a lock should be injected when some named member/type is used.
    // Declared on a patch class and provided with the patch's original class, the attribute will by default add locks on the instance to every instance method.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireLockAttribute : Attribute
    {
        internal const int invalidIndex = -1;

        internal static void EnsureScope()
        {
            // TODO: find every class marked with this attribute, raise errors for any enclosingType that doesn't have
            // a member with the given name, or where lockType does not match what is discovered.
        }

        public readonly Type enclosingType;
        public readonly LockType lockType;
        public readonly int? paramIndex;
        public readonly string memberName;

        public RequireLockAttribute(Type enclosingType,
                                    LockType lockType = LockType.Instance,
                                    int paramIndex = invalidIndex,
                                    string memberName = null)
        {
            this.enclosingType = enclosingType;
            this.lockType = lockType;
            this.paramIndex = paramIndex;
            this.memberName = memberName;
        }

        public enum LockType
        {
            None,
            Instance,
            Parameter,
            Field
        }
    }
}
