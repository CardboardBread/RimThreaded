using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace RimThreaded.Utilities;

public static class AttributeUtility
{
    // For working with `ILocationAware`, `out var` technique for casting the member from `ILocationAware._Locate()`.
    [Obsolete]
    private static bool TryLocation<TMember>(MemberInfo member, out TMember location) where TMember : MemberInfo
    {
        if (member is TMember cast)
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
    [Obsolete]
    private static bool AssertLocation<TMember>(MemberInfo member, out TMember location) where TMember : MemberInfo
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

    [Obsolete]
    private static IEnumerable<TLocationAwareAttribute> GetLocalUsages<TLocationAwareAttribute>() where TLocationAwareAttribute : Attribute, ILocationAware
    {
        var types = from type in typeof(AttributeUtility).Assembly.GetTypes().AsParallel()
            select type;

        foreach (var type in types)
        {
            if (type.TryGetAttribute<TLocationAwareAttribute>(out var tReplace))
            {
                tReplace.Locate(type);
                yield return tReplace;
            }

            foreach (var method in AccessTools.GetDeclaredMethods(type))
            {
                if (method.TryGetAttribute<TLocationAwareAttribute>(out var mReplace))
                {
                    mReplace.Locate(method);
                    yield return mReplace;
                }
            }

            foreach (var field in AccessTools.GetDeclaredFields(type))
            {
                if (field.TryGetAttribute<TLocationAwareAttribute>(out var fReplace))
                {
                    fReplace.Locate(field);
                    yield return fReplace;
                }
            }
        }
    }

    [Obsolete]
    private static readonly Dictionary<(Type, BindingFlags), IEnumerable<MethodInfo>> _cachedLocalUsages = new();

    [Obsolete]
    private static IEnumerable<MethodInfo> GetLocalUsageMethods<TAttribute>(BindingFlags bindingFlags = BindingFlags.Static) where TAttribute : Attribute
    {
        // TODO: verify A has the AttributeUsage meta attribute with AttributeTargets.Method
        var cachePair = (typeof(TAttribute), bindingFlags);
        if (!_cachedLocalUsages.TryGetValue(cachePair, out var methods))
        {
            methods = from type in typeof(AttributeUtility).Assembly.GetTypes()
                from method in type.GetMethods(bindingFlags)
                where method.HasAttribute<TAttribute>()
                select method;
            _cachedLocalUsages[cachePair] = methods;
        }

        return methods;
    }

    [Obsolete]
    private static void InvokeAllUsageMethods<TAttribute>(IEnumerable<MethodInfo> methods) where TAttribute : Attribute
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
                InvokeUsageMethod<TAttribute>(method);
            }
            catch (Exception ex)
            {
                RTLog.Error($"Encountered error while invoking {typeof(TAttribute)} usage method: {ex}");
            }
        }
    }

    [Obsolete]
    private static void InvokeUsageMethod<TAttribute>(MethodInfo method) where TAttribute : Attribute
    {
        // TODO: verify A has the AttributeUsage meta attribute with AttributeTargets.Method
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        if (!method.IsStatic)
        {
            throw new ArgumentException($"{typeof(TAttribute)} usage method '{method}' must be static to be invoked.", nameof(method));
        }

        if (method.GetParameters().Length != 0)
        {
            throw new ArgumentException($"{typeof(TAttribute)} usage method '{method}' has a non-zero number of parameters, and cannot be invoked.", nameof(method));
        }

        method.Invoke(null, null);
    }
}