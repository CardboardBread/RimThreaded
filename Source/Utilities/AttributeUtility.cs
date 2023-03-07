using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace RimThreaded.Utilities
{
    public static class AttributeUtility
    {
        public static IEnumerable<A> GetLocalUsages<A>() where A : Attribute, ILocationAware
        {
            var types = from type in RimThreadedMod.LocalTypes.AsParallel()
                        select type;

            foreach (var type in types)
            {
                if (type.TryGetAttribute<A>(out var tReplace))
                {
                    tReplace.Locate(type);
                    yield return tReplace;
                }

                foreach (var method in AccessTools.GetDeclaredMethods(type))
                {
                    if (method.TryGetAttribute<A>(out var mReplace))
                    {
                        mReplace.Locate(method);
                        yield return mReplace;
                    }
                }

                foreach (var field in AccessTools.GetDeclaredFields(type))
                {
                    if (field.TryGetAttribute<A>(out var fReplace))
                    {
                        fReplace.Locate(field);
                        yield return fReplace;
                    }
                }
            }
        }

        private static readonly Dictionary<(Type, BindingFlags), IEnumerable<MethodInfo>> _cachedLocalUsages = new();

        public static IEnumerable<MethodInfo> GetLocalUsageMethods<A>(BindingFlags bindingFlags = BindingFlags.Static) where A : Attribute
        {
            // TODO: verify A has the AttributeUsage meta attribute with AttributeTargets.Method
            var cachePair = (typeof(A), bindingFlags);
            if (!_cachedLocalUsages.TryGetValue(cachePair, out var methods))
            {
                methods = from type in RimThreadedMod.LocalTypes
                          from method in type.GetMethods(bindingFlags)
                          where method.HasAttribute<A>()
                          select method;
                _cachedLocalUsages[cachePair] = methods;
            }

            return methods;
        }

        public static void InvokeAllUsageMethods<A>(IEnumerable<MethodInfo> methods) where A : Attribute
        {
            // TODO: verify A has the AttributeUsage meta attribute with AttributeTargets.Method
            if (methods is null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            foreach (var method in methods)
            {
                try
                {
                    InvokeUsageMethod<A>(method);
                }
                catch (Exception ex)
                {
                    Log.Error($"Encountered error while invoking {typeof(A)} usage method: {ex}");
                }
            }
        }

        public static void InvokeUsageMethod<A>(MethodInfo method) where A : Attribute
        {
            // TODO: verify A has the AttributeUsage meta attribute with AttributeTargets.Method
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (!method.IsStatic)
            {
                throw new ArgumentException($"{typeof(A)} usage method '{method}' must be static to be invoked.", nameof(method));
            }

            if (method.GetParameters().Length != 0)
            {
                throw new ArgumentException($"{typeof(A)} usage method '{method}' has a non-zero number of parameters, and cannot be invoked.", nameof(method));
            }

            method.Invoke(null, null);
        }
    }
}
