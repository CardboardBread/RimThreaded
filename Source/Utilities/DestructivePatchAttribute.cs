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
    // Instead of ensuring all the required RunDestructivePatches are called, just mark them and let the environment
    // find them all.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class DestructivePatchAttribute : Attribute
    {
        public static IEnumerable<MethodInfo> AllTargets
            = MethodAttributeUtility.GetStaticAttributeTargets<DestructivePatchAttribute>();

        public static void RunAllDestructivePatches()
            => MethodAttributeUtility.RunAllAttributeTargets<DestructivePatchAttribute>(AllTargets, targetName: "Destructive Patch");
    }
}
