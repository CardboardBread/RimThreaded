using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patches
{
    public static class Patch_CacheInstanceMethodCall
    {
        internal static MemoryCache cache = new(nameof(Patch_CacheStaticMethodCall));
        internal static ConditionalWeakTable<object, string> cacheNames = new();

        [HarmonyPrepare]
        public static void Prepare(Harmony harmony, MethodBase original = null)
        {
            if (original == null)
            {

            }

            if (original != null)
            {

            }
        }

        // Every instance method in the game that returns an object or something that can be cast to an object.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            foreach (var target in from type in typeof(Game).Assembly.GetTypes().AsParallel()
                                   from method in type.GetMethods(AccessTools.allDeclared)
                                   where !method.IsStatic
                                   where method.ReturnType != typeof(void)
                                   where typeof(object).IsAssignableFrom(method.ReturnType)
                                   select method)
            {
                yield return target;
            }

            foreach (var target in from type in typeof(Game).Assembly.GetTypes().AsParallel()
                                   from property in type.GetProperties(AccessTools.allDeclared)
                                   where property.GetMethod != null
                                   select property.GetMethod into getter
                                   where !getter.IsStatic
                                   where getter.ReturnType != typeof(void)
                                   where typeof(object).IsAssignableFrom(getter.ReturnType)
                                   select getter)
            {
                yield return target;
            }

            foreach (var target in from type in typeof(Game).Assembly.GetTypes().AsParallel()
                                   from property in type.GetProperties(AccessTools.allDeclared)
                                   where property.SetMethod != null
                                   select property.SetMethod into setter
                                   where !setter.IsStatic
                                   where setter.ReturnType != typeof(void)
                                   where typeof(object).IsAssignableFrom(setter.ReturnType)
                                   select setter)
            {
                yield return target;
            }
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, ref object __result, object[] __args, MethodBase __originalMethod)
        {
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance, ref object __result, object[] __args, MethodBase __originalMethod, bool __runOriginal)
        {

        }

        [HarmonyFinalizer]
        public static void Finalizer(Exception __exception, object __instance, object[] __args, MethodBase __originalMethod)
        {

        }

        [HarmonyCleanup]
        public static void Cleanup(Harmony harmony, MethodBase original = null, Exception ex = null)
        {

        }
    }
}
