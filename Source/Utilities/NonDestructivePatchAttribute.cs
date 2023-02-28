using System;
using System.Collections.Generic;
using System.Reflection;

namespace RimThreaded.Utilities
{
    // Same as DestructivePatchAttribute but opposite kind of methods.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class NonDestructivePatchAttribute : Attribute
    {
        public static IEnumerable<MethodInfo> AllTargets
            = MethodAttributeUtility.GetStaticAttributeTargets<NonDestructivePatchAttribute>();

        public static void RunAllNonDestructivePatches()
            => MethodAttributeUtility.RunAllAttributeTargets<NonDestructivePatchAttribute>(AllTargets, targetName: "Non Destructive Patch");
    }
}
