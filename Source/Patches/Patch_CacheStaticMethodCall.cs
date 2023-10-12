using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Patches
{
    // While System.Runtime.Caching.MemoryCache is a bit expensive per entry, its awareness of cache-specific or application-wide memory limits will
    // help avoid flooding/stalling memory with cached method results.
    [HarmonyPatch]
    public static class Patch_CacheStaticMethodCall
    {
        [HarmonyPatch]
        public static class PostTickEviction
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
            public static void Postfix_Root_Play_Update()
            {
                foreach (var entry in from key in reverseMethodEvictionFrequency.Keys
                                          where Find.TickManager.TicksGame % key == 0
                                          select reverseEntryEvictionFrequency[key] into entries
                                          from entry in entries
                                          select entry)
                {
                    RemoveEntry(entry);
                }
            }
        }

        private static MemoryCache resultCache = new(nameof(Patch_CacheStaticMethodCall));
        private static CacheItemPolicy itemPolicy = new()
        {
            UpdateCallback = CacheEntryUpdateCallback,
            RemovedCallback = CacheEntryRemovedCallback,
        };

        private static readonly Dictionary<MethodBase, int> methodEvictionFrequency = new();
        private static readonly Dictionary<string, MethodBase> entryMethod = new();

        // 'computed' dictionaries, reducing search operations to a single dict access
        private static readonly HashSet<MethodBase> knownMethods = new();
        private static readonly HashSet<int> knownEvictionFrequencies = new();
        private static readonly Dictionary<int, HashSet<MethodBase>> reverseMethodEvictionFrequency = new();
        private static readonly HashSet<string> knownEntries = new();
        private static readonly Dictionary<string, int> entryEvictionFrequency = new();
        private static readonly Dictionary<int, HashSet<string>> reverseEntryEvictionFrequency = new();
        private static readonly Dictionary<MethodBase, HashSet<string>> reverseEntryMethod = new();

        private static void CacheEntryUpdateCallback(CacheEntryUpdateArguments arguments)
        {
        }

        private static void CacheEntryRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            UntrackEntry(arguments.CacheItem.Key);
        }

        private static int GetCallHash(MethodBase methodBase, params object[] arguments)
        {
            if (methodBase is null)
            {
                throw new ArgumentNullException(nameof(methodBase));
            }

            if (arguments == null)
            {
                return HashCode.Combine(methodBase);
            }

            return arguments.Length switch
            {
                0 => HashCode.Combine(methodBase),
                1 => HashCode.Combine(methodBase, arguments[0]),
                2 => HashCode.Combine(methodBase, arguments[0], arguments[1]),
                3 => HashCode.Combine(methodBase, arguments[0], arguments[1], arguments[2]),
                4 => HashCode.Combine(methodBase, arguments[0], arguments[1], arguments[2], arguments[3]),
                5 => HashCode.Combine(methodBase, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]),
                6 => HashCode.Combine(methodBase, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]),
                7 => HashCode.Combine(methodBase, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6]),
                _ => buildCallHash(methodBase, arguments),
            };

            static int buildCallHash(MethodBase methodBase, object[] arguments)
            {
                var hash = new HashCode();
                hash.Add(methodBase);
                foreach (var arg in arguments)
                {
                    hash.Add(arg);
                }
                return hash.ToHashCode();
            }
        }

        private static void InitializeMethodCaching(MethodBase methodBase, int evictionFrequency)
        {
            if (!knownMethods.Add(methodBase))
            {
                throw new ArgumentException($"Method {methodBase} is already handled");
            }

            reverseEntryMethod[methodBase] = new();
            if (knownEvictionFrequencies.Add(evictionFrequency))
            {
                reverseMethodEvictionFrequency[evictionFrequency] = new();
                reverseEntryEvictionFrequency[evictionFrequency] = new();
            }

            methodEvictionFrequency[methodBase] = evictionFrequency;
            reverseMethodEvictionFrequency[evictionFrequency].Add(methodBase);
        }

        private static void UpdateMethodCaching(MethodBase methodBase, int evictionFrequency)
        {
            if (!knownMethods.Contains(methodBase))
            {
                throw new ArgumentException($"Method {methodBase} is not handled");
            }

            throw new NotImplementedException();
        }

        private static void AddEntry(string entry, object value, MethodBase methodBase)
        {
            var frequency = methodEvictionFrequency[methodBase];
            resultCache.Set(entry, value, itemPolicy);
            entryMethod[entry] = methodBase;
            knownEntries.Add(entry);
            entryEvictionFrequency[entry] = frequency;
            reverseEntryEvictionFrequency[frequency].Add(entry);
            reverseEntryMethod[methodBase].Add(entry);
        }

        private static void RemoveEntry(string entry, MethodBase methodBase = null, int evictionFrequency = 0)
        {
            _ = resultCache.Remove(entry);
            UntrackEntry(entry, methodBase, evictionFrequency);
        }

        private static void UntrackEntry(string entry, MethodBase methodBase = null, int evictionFrequency = 0)
        {
            knownEntries.Remove(entry);
            entryEvictionFrequency.Remove(entry);

            methodBase = entryMethod[entry];
            entryMethod.Remove(entry);
            reverseEntryMethod[methodBase].Remove(entry);

            evictionFrequency = entryEvictionFrequency[entry];
            entryEvictionFrequency.Remove(entry);
            reverseEntryEvictionFrequency[evictionFrequency].Remove(entry);
        }

        [HarmonyPrepare]
        public static void Prepare(Harmony harmony, MethodBase original = null)
        {
            if (original == null)
            {
                itemPolicy.SlidingExpiration = TimeSpan.FromMilliseconds(RimThreadedSettings.Instance.TimeoutMilliseconds);
            }

            if (original != null)
            {
                InitializeMethodCaching(original, 1);
            }
        }

        // Every static method in the game that returns an object or something that can be cast to an object.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            foreach (var target in from type in typeof(Game).Assembly.GetTypes().AsParallel()
                                   from method in type.GetMethods(AccessTools.allDeclared)
                                   where method.IsStatic
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
                                   where getter.IsStatic
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
                                   where setter.IsStatic
                                   where setter.ReturnType != typeof(void)
                                   where typeof(object).IsAssignableFrom(setter.ReturnType)
                                   select setter)
            {
                yield return target;
            }
        }

        // Get cached result and cancel original method, or do nothing and let it run.
        [HarmonyPrefix]
        public static bool Prefix(ref object __result, object[] __args, MethodBase __originalMethod)
        {
            var identity = GetCallHash(__originalMethod, __args).ToString();

            if (resultCache.Get(identity) is object cacheValue)
            {
                __result = cacheValue;
                return false;
            }

            return true;
        }

        // Cache the result if it was generated by the original method.
        [HarmonyPostfix]
        public static void Postfix(ref object __result, object[] __args, MethodBase __originalMethod, bool __runOriginal)
        {
            if (__runOriginal)
            {
                var identity = GetCallHash(__originalMethod, __args).ToString();
                AddEntry(identity, __result, __originalMethod);
            }
        }

        [HarmonyFinalizer]
        public static void Finalizer(Exception __exception, ref object __result, object[] __args, MethodBase __originalMethod, bool __runOriginal)
        {
            //var identity = GetCallHash(__originalMethod, __args).ToString();
            //RemoveEntry(identity);
        }

        [HarmonyCleanup]
        public static void Cleanup(Harmony harmony, MethodBase original = null, Exception ex = null)
        {

        }
    }
}
