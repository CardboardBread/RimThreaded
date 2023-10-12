using RimThreaded.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Assertions;

namespace RimThreaded.Patching
{
    // Utility class for caching the results of method invocations
    public static class MethodCallCache
    {
        [HarmonyPatch]
        public static class PostTickEviction
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
            public static void Postfix_Root_Play_Update()
            {
                foreach (var entry in from pair in staticEntries
                                      where Find.TickManager.TicksGame % methodEvictionFrequency[pair.Value] == 0
                                      select pair.Key)
                {
                    RemoveEntry(entry);
                }
            }
        }

        public static readonly MemoryCache ResultCache = new(nameof(MethodCallCache));
        private static readonly CacheItemPolicy itemPolicy = new()
        {
            UpdateCallback = CacheEntryUpdateCallback,
            RemovedCallback = CacheEntryRemovedCallback,
        };

        private static readonly Dictionary<MethodBase, int> methodEvictionFrequency = new();
        private static readonly Dictionary<string, MethodBase> staticEntries = new();
        private static readonly ConditionalWeakTable<object, string> instanceEntries = new();
        private static readonly ConditionalWeakTable<object, MethodBase> instanceSource = new();

        // 'computed' dictionaries, reducing search operations to a single dict access
        //private static readonly HashSet<MethodBase> knownMethods = new();
        //private static readonly HashSet<int> knownEvictionFrequencies = new();
        //private static readonly Dictionary<int, HashSet<MethodBase>> reverseMethodEvictionFrequency = new();
        //private static readonly HashSet<string> knownEntries = new();
        //private static readonly Dictionary<string, int> entryEvictionFrequency = new();
        //private static readonly Dictionary<int, HashSet<string>> reverseEntryEvictionFrequency = new();
        //private static readonly Dictionary<MethodBase, HashSet<string>> reverseEntryMethod = new();

        private static void CacheEntryUpdateCallback(CacheEntryUpdateArguments arguments)
        {
        }

        private static void CacheEntryRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            UntrackEntry(arguments.CacheItem.Key);
        }

        public static int GetStaticCallHash(MethodBase method, params object[] arguments)
        {
            Assert.IsTrue(method.IsStatic);

            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (arguments == null)
            {
                return HashCode.Combine(method);
            }

            return arguments.Length switch
            {
                0 => HashCode.Combine(method),
                1 => HashCode.Combine(method, arguments[0]),
                2 => HashCode.Combine(method, arguments[0], arguments[1]),
                3 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2]),
                4 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3]),
                5 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]),
                6 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]),
                7 => HashCode.Combine(method, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6]),
                _ => BuildStaticCallHash(method, arguments),
            };
        }

        private static int BuildStaticCallHash(MethodBase method, object[] arguments)
        {
            Assert.IsTrue(method.IsStatic);

            var hash = new HashCode();
            hash.Add(method);
            foreach (var arg in arguments)
            {
                hash.Add(arg);
            }
            return hash.ToHashCode();
        }

        public static int GetInstanceCallHash(MethodBase method, object instance, params object[] arguments)
        {
            Assert.IsFalse(method.IsStatic);

            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (arguments == null)
            {
                return HashCode.Combine(method, instance);
            }

            return arguments.Length switch
            {
                0 => HashCode.Combine(method, instance),
                1 => HashCode.Combine(method, instance, arguments[0]),
                2 => HashCode.Combine(method, instance, arguments[0], arguments[1]),
                3 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2]),
                4 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2], arguments[3]),
                5 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]),
                6 => HashCode.Combine(method, instance, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]),
                _ => BuildInstanceCallHash(method, instance, arguments),
            };
        }

        private static int BuildInstanceCallHash(MethodBase method, object instance, object[] arguments)
        {
            Assert.IsFalse(method.IsStatic);

            var hash = new HashCode();
            hash.Add(method);
            hash.Add(instance);
            foreach (var arg in arguments)
            {
                hash.Add(arg);
            }
            return hash.ToHashCode();
        }

        public static void InitializeMethodCaching(MethodBase method, int evictionFrequency)
        {
            if (methodEvictionFrequency.ContainsKey(method))
            {
                throw new ArgumentException($"Method {method} is already handled");
            }

            methodEvictionFrequency[method] = evictionFrequency;
        }

        public static void UpdateMethodCaching(MethodBase methodBase, int evictionFrequency)
        {
            if (!methodEvictionFrequency.ContainsKey(methodBase))
            {
                throw new ArgumentException($"Method {methodBase} is not handled");
            }

            throw new NotImplementedException();
        }

        public static object GetEntry(string entry)
        {
            return ResultCache.Get(entry);
        }

        public static bool HasEntry(string entry) => ResultCache.Contains(entry);

        public static void AddEntry(string entry, object value, MethodBase methodBase)
        {
            var frequency = methodEvictionFrequency[methodBase];
            ResultCache.Set(entry, value, itemPolicy);
            staticEntries[entry] = methodBase;
        }

        public static void RemoveEntry(string entry, MethodBase methodBase = null, int evictionFrequency = 0)
        {
            _ = ResultCache.Remove(entry);
            UntrackEntry(entry, methodBase, evictionFrequency);
        }

        public static void UntrackEntry(string entry, MethodBase methodBase = null, int evictionFrequency = 0)
        {
            knownEntries.Remove(entry);
            entryEvictionFrequency.Remove(entry);

            methodBase = staticEntries[entry];
            staticEntries.Remove(entry);
            reverseEntryMethod[methodBase].Remove(entry);

            evictionFrequency = entryEvictionFrequency[entry];
            entryEvictionFrequency.Remove(entry);
            reverseEntryEvictionFrequency[evictionFrequency].Remove(entry);
        }
    }
}
