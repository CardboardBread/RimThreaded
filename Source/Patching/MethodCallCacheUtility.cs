using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace RimThreaded.Patching;

/// <summary>
/// Utility class for caching the results of method invocations.
/// </summary>
public static class MethodCallCacheUtility
{
    public record struct MethodCallCacheEntry(int Identity, MethodBase Method, int Eviction, int CreationTick);
    
    /// <summary>
    /// Generates a hashcode from a call to a static method, for deriving a unique identity per unique method call.
    /// </summary>
    public static int GetStaticCallHash([NotNull] MethodBase method, [CanBeNull] params object[] arguments)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        if (!method.IsStatic) throw new ArgumentException($"Method {method} must be static", nameof(method));

        // For calls to methods with 7 or less parameters, use the available HashCode.Combine overloads.
        if (arguments == null) return HashCode.Combine(method);
        return arguments.Length switch
        {
            0 => HashCode.Combine(method),
            1 => HashCode.Combine(method, arguments[0]),
            2 => HashCode.Combine(method, arguments[0], arguments[1]),
            3 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2]),
            4 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3]),
            5 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]),
            6 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4],
                arguments[5]),
            7 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4],
                arguments[5], arguments[6]),
            _ => BuildStaticCallHash(method, arguments),
        };
    }

    /// <summary>
    /// For calls to static methods with more than 7 parameters.
    /// </summary>
    private static int BuildStaticCallHash([NotNull] MethodBase method, [NotNull] object[] arguments)
    {
        var hash = new HashCode();
        hash.Add(method);
        foreach (var arg in arguments) hash.Add(arg);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Generates a hashcode from a call to an instance method, for deriving a unique identity per unique method call.
    /// </summary>
    public static int GetInstanceCallHash([NotNull] MethodBase method, [NotNull] object instance,
        [CanBeNull] params object[] arguments)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        if (method.IsStatic) throw new ArgumentException($"Method {method} cannot be static", nameof(method));

        // For calls to methods with 6 or less parameters, use the available HashCode.Combine overloads.
        if (arguments == null) return HashCode.Combine(method, instance);
        return arguments.Length switch
        {
            0 => HashCode.Combine(method, instance),
            1 => HashCode.Combine(method, instance, arguments[0]),
            2 => HashCode.Combine(method, instance, arguments[0], arguments[1]),
            3 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2]),
            4 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2], arguments[3]),
            5 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2], arguments[3],
                arguments[4]),
            6 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2], arguments[3],
                arguments[4], arguments[5]),
            _ => BuildInstanceCallHash(method, instance, arguments),
        };
    }

    /// <summary>
    /// For calls to instance methods with more than 6 parameters.
    /// </summary>
    private static int BuildInstanceCallHash([NotNull] MethodBase method, [NotNull] object instance,
        [NotNull] object[] arguments)
    {
        var hash = new HashCode();
        hash.Add(method);
        hash.Add(instance);
        foreach (var arg in arguments) hash.Add(arg);
        return hash.ToHashCode();
    }
    
    public static void AddStaticEntry(int identity, object value, [NotNull] MethodBase method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        if (!IsMethodHandled(method)) throw new ArgumentException($"Method {method} is not handled", nameof(method));
        if (!method.IsStatic) throw new ArgumentException("Cannot track non-static method cache without instance");

        AddStaticEntryInternal(identity, value, method);
    }

    internal static void AddStaticEntryInternal(int identity, object value, MethodBase method)
    {
        var eviction = MethodEvictions[method];
        var currentTick = Find.TickManager.TicksGame;

        StaticResultCache.Set(identity.ToString(), value, StaticItemPolicy);
        StaticEntries[identity] = new(identity, method, eviction, currentTick);
    }

    public static void AddInstanceEntry(int identity, object value, [NotNull] object instance,
        [NotNull] MethodBase method)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        if (!IsMethodHandled(method)) throw new ArgumentException($"Method {method} is not handled", nameof(method));
        if (method.IsStatic) throw new ArgumentException("Cannot track static method with instance");

        AddInstanceEntryInternal(identity, value, instance, method);
    }

    internal static void AddInstanceEntryInternal(int identity, object value, [NotNull] object instance,
        [NotNull] MethodBase method)
    {
        var eviction = MethodEvictions[method];
        var currentTick = Find.TickManager.TicksGame;
        var entrySet = InstanceEntries.GetValue(instance, key => new());

        InstanceResultCache.Set(identity.ToString(), value, InstanceItemPolicy);
        var cacheEntry = new MethodCallCacheEntry(identity, method, eviction, currentTick);
        entrySet.Add(cacheEntry);
    }

    public static void RemoveStaticEntry(string entry, MethodBase methodBase = null, int evictionFrequency = 0)
    {
        _ = StaticResultCache.Remove(entry);
        UntrackEntry(entry, methodBase, evictionFrequency);
    }

    public static void UntrackEntry(string entry, MethodBase methodBase = null, int evictionFrequency = 0)
    {
        knownEntries.Remove(entry);
        entryEvictionFrequency.Remove(entry);

        methodBase = StaticEntries[entry];
        StaticEntries.Remove(entry);
        reverseEntryMethod[methodBase].Remove(entry);

        evictionFrequency = entryEvictionFrequency[entry];
        entryEvictionFrequency.Remove(entry);
        reverseEntryEvictionFrequency[evictionFrequency].Remove(entry);
    }
}