using System;
using System.Collections.Generic;
using System.Reflection;

namespace RimThreaded.Utilities
{
    // Same as DestructivePatchAttribute but for the methods that initialize thread static variables.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ThreadStaticInitializerAttribute : Attribute
    {
        public static IEnumerable<MethodInfo> AllTargets
            = MethodAttributeUtility.GetStaticAttributeTargets<ThreadStaticInitializerAttribute>();

        public static void RunAllThreadStaticInitializers()
            => MethodAttributeUtility.RunAllAttributeTargets<ThreadStaticInitializerAttribute>(AllTargets, targetName: "Thread Static Initializer");
    }
}
