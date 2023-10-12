using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Verse;

namespace RimThreaded.Utilities
{
    public static class AttributeUtility
    {
        private static bool IsStatic(this MemberInfo member)
        {
            return member switch
            {
                Type type => type.GetConstructors(AccessTools.all).Count() == 0 && type.BaseType == typeof(object),
                FieldInfo field => field.IsStatic,
                ConstructorInfo constructor => constructor.IsStatic,
                MethodInfo method => method.IsStatic,
                PropertyInfo property => isPropertyStatic(property),
                _ => false
            };

            static bool isPropertyStatic(PropertyInfo property)
            {
                return (property.GetGetMethod(true)?.IsStatic ?? true) && (property.GetSetMethod(true)?.IsStatic ?? true);
            }
        }

        // For working with `ILocationAware`, `out var` technique for casting the member from `ILocationAware._Locate()`.
        private static bool TryLocation<T>(MemberInfo member, out T location) where T : MemberInfo
        {
            if (member is T cast)
            {
                location = cast;
                return true;
            }
            else
            {
                location = null;
                return false;
            }
        }

        // Same as `AttributeUtility.TryLocation` but throwing excepting on failure.
        private static bool AssertLocation<T>(MemberInfo member, out T location) where T : MemberInfo
        {
            if (member is null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            if (TryLocation(member, out location))
            {
                return true;
            }
            else
            {
                throw new ArgumentException("ILocationAware Attribute was located on unsupported member.");
            }
        }

        private static IEnumerable<A> GetLocalUsages<A>() where A : Attribute, ILocationAware
        {
            var types = from type in RimThreaded.LocalTypes.AsParallel()
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

        private static IEnumerable<MethodInfo> GetLocalUsageMethods<A>(BindingFlags bindingFlags = BindingFlags.Static) where A : Attribute
        {
            // TODO: verify A has the AttributeUsage meta attribute with AttributeTargets.Method
            var cachePair = (typeof(A), bindingFlags);
            if (!_cachedLocalUsages.TryGetValue(cachePair, out var methods))
            {
                methods = from type in RimThreaded
.LocalTypes
                          from method in type.GetMethods(bindingFlags)
                          where method.HasAttribute<A>()
                          select method;
                _cachedLocalUsages[cachePair] = methods;
            }

            return methods;
        }

        private static void InvokeAllUsageMethods<A>(IEnumerable<MethodInfo> methods) where A : Attribute
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
                    RTLog.Error($"Encountered error while invoking {typeof(A)} usage method: {ex}");
                }
            }
        }

        private static void InvokeUsageMethod<A>(MethodInfo method) where A : Attribute
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

    /// <summary>Thrown to indicate an attribute is declared on an incompatible member, beyond the compile-time validation of System.AttributeUsage.</summary>
    public class AttributeUsageException : Exception
    {
        public AttributeUsageException()
        {
        }

        public AttributeUsageException(string message) : base(message)
        {
        }

        public AttributeUsageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AttributeUsageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
