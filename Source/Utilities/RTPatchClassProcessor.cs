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
    public class RTPatchClassProcessor : PatchClassProcessor
    {
        public readonly string category;

        public RTPatchClassProcessor(Harmony instance, Type type, string category) : base(instance, type)
        {
            this.category = category;

            var allMethods = type.GetMethods(AccessTools.all).ToList();

            // Find a categorized version of each auxillary function, and remove any existing
            RebindHarmonyFunction<HarmonyPrepare>(allMethods);
            RebindHarmonyFunction<HarmonyCleanup>(allMethods);
            RebindHarmonyFunction<HarmonyTargetMethod>(allMethods);
            RebindHarmonyFunction<HarmonyTargetMethods>(allMethods);

            foreach (var patch in patchMethods)
            {
                if (!IsPatchTargeted(patch))
                {
                    patchMethods.Remove(patch);
                }
            }
        }

        private bool RebindHarmonyFunction<A>(IEnumerable<MethodInfo> methods = null) where A : Attribute
        {
            methods ??= containerType.GetMethods(AccessTools.all);

            if (methods.FirstOrDefault(IsHarmonyFunction<A>) is MethodInfo method)
            {
                auxilaryMethods[typeof(A)] = method;
                return true;
            }
            else
            {
                auxilaryMethods.Remove(typeof(A));
                return false;
            }
        }

        private bool IsHarmonyFunction<A>(MethodInfo method) where A : Attribute
        {
            return method.HasAttribute<A>()
                && IsMethodTargeted(method);
        }

        private bool IsHarmonyFunction<A>(MethodInfo method, out PatchCategory patchCategory) where A : Attribute
        {
            patchCategory = method.GetCustomAttribute<PatchCategory>();
            return IsHarmonyFunction<A>(method);
        }

        private bool IsMethodTargeted(MethodInfo method)
        {
            return method.HasAttribute<PatchCategory>()
                && method.GetCustomAttribute<PatchCategory>() is PatchCategory patchCategory
                && patchCategory.category == category;
        }

        private bool IsPatchTargeted(AttributePatch patch)
        {
            return IsMethodTargeted(patch.info.method);
        }
    }
}
