using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace RimThreaded;

// A couple methods here do the same as HarmonyLib.AccessTools, these copies are to avoid the creation of wasted Lists.
public static class Extensions
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }

    public static ICollection<T> AsCollection<T>(this IEnumerable<T> source)
    {
        if (source is ICollection<T> collection) return collection;
        return source.ToList();
    }

    public static IEnumerable<MethodBase> AllMethodBases(this Type type)
    {
        foreach (var method in type.GetMethods(AccessTools.allDeclared)) yield return method;
        foreach (var constructor in type.GetConstructors(AccessTools.allDeclared)) yield return constructor;

        // TODO: verify these methods aren't picked up by type.GetMethods(AccessTools.allDeclared))
        foreach (var property in type.GetProperties(AccessTools.allDeclared))
        {
            if (property.GetMethod is { } getMethod) yield return getMethod;
            if (property.SetMethod is { } setMethod) yield return setMethod;
        }
    }

    private static readonly ConditionalWeakTable<MethodBase, string> TraceSignatures = new();

    /// <summary>
    /// Get or construct a unique and human-readable string representing this <see cref="MethodBase"/>.
    /// </summary>
    public static string GetTraceSignature(this MethodBase method) => TraceSignatures.GetValue(method, BuildTraceSignature);

    /// <summary>
    /// Convert a <see cref="MethodBase"/> to a unique and human-readable string.
    /// </summary>
    private static string BuildTraceSignature(MethodBase method)
    {
        var paramNames = method.GetParameters().Select(p => p.ParameterType.FullName + " " + p.Name);
        var joinedParams = string.Join(", ", paramNames);
            
        var generics = BuildGenericsSignature(method);

        var declaringPrefix = method.DeclaringType is not null ? method.DeclaringType.FullName + "." : "";
            
        var signature = declaringPrefix + method.Name + generics + "(" + joinedParams + ")";
        return signature;
    }

    private static string BuildGenericsSignature(MethodBase method)
    {
        var genericArguments = method.GetGenericArguments();
        if (!genericArguments.Any()) return string.Empty;
                
        var genericNames = genericArguments.Select(g => g.FullName);
        var joinedGenerics = string.Join(", ", genericNames);
        return "<" + joinedGenerics + ">";
    }
}