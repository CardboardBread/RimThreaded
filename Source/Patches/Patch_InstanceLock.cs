using RimThreaded.Patching;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace RimThreaded.Patches
{
    // Harmony patch class for performing instance locks that wrap instance methods, using Harmony Prefixes and Finalizers.
    [HarmonyPatch]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    public static class Patch_InstanceLock
    {
        public const int PriorityOffset = 0;

        private static ConcurrentDictionary<object, Thread> owners = new();
        private static Dictionary<MethodBase, RequireLockAllAttribute> multiLockConfig = new();
        private static Dictionary<MethodBase, RequireLockPatchAttribute> singleLockConfig = new();

        [HarmonyPrepare]
        public static void Prepare(Harmony harmony)
        {
        }

        // Every instanced class marked as explicitly needing instance locks and not any explicitly marked for ignoring.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            foreach(var requireLock in from attribute in RimThreadedMod.Instance.GetLocalAttributesByType<RequireLockPatchAttribute>()
                                       where attribute.Target != null
                                       where attribute.LockType == RequireLockType.Instance
                                       where attribute.WrapMethod
                                       select attribute)
            {
                singleLockConfig[requireLock.Target] = requireLock;
                yield return requireLock.Target;
            }

            foreach(var requireLockAll in from attribute in RimThreadedMod.Instance.GetLocalAttributesByType<RequireLockAllAttribute>()
                                          where attribute.LockType == RequireLockType.Instance
                                          where attribute.WrapMethod
                                          select attribute)
            {
                foreach (var method in (requireLockAll.Parent as Type))
                {
                    multiLockConfig[method] = requireLockAll;
                    yield return method;
                }
            }

            foreach (var localType in RimThreaded.LocalTypes)
            {
                foreach (var attribute in localType.GetCustomAttributes(inherit: true))
                {
                    if (attribute is RequireLockPatchAttribute requireLock && requireLock.Target != null && requireLock.LockType == RequireLockType.Instance && requireLock.WrapMethod)
                    {
                        singleLockConfig[requireLock.Target] = requireLock;
                        yield return requireLock.Target;
                    }

                    if (attribute is RequireLockAllAttribute requireLockAll && requireLockAll.LockType == RequireLockType.Instance && requireLockAll.WrapMethod)
                    {
                        foreach (var method in AccessTools.GetDeclaredMethods(localType))
                        {
                            multiLockConfig[method] = requireLockAll;
                            yield return method;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last + PriorityOffset)] // This patch should run right before the original method.
        public static void Prefix(object __instance)
        {
            if (__instance is null)
            {
                throw new ArgumentNullException(nameof(__instance));
            }

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(__instance, RimThreadedSettings.Instance.TimeoutMilliseconds, ref lockTaken);
                if (lockTaken)
                {
                    owners.TryAdd(__instance, Thread.CurrentThread);
                    return;
                }
            }
            finally
            {
                if (!lockTaken)
                {
                    Log.Warning($"Failed to acquire lock on {__instance}");
                }
            }
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.First - PriorityOffset)] // This patch should run as close to the end of the original method as possible.
        public static void Finalizer(object __instance, Exception __exception)
        {
            if (__instance is null)
            {
                throw new ArgumentNullException(nameof(__instance));
            }

            if (Monitor.IsEntered(__instance))
            {
                owners.TryRemove(__instance, out var owner);
                Monitor.Exit(__instance);
            }
            else
            {
                Log.Warning($"Lock for {__instance} was unacquired before {typeof(Patch_InstanceLock)}");
            }
        }

        [HarmonyCleanup]
        public static void Cleanup(Harmony harmony)
        {
            var RTHarm = (RimThreadedHarmony)harmony;
            singleLockConfig.Clear();
            multiLockConfig.Clear();
        }
    }
}
