using HarmonyLib;
using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Verse;

namespace RimThreaded.Patches.VersePatches;

// TODO: 
[HarmonyPatch(typeof(GenTypes))]
public static class GenTypes_Patch
{
    [HarmonyPatch(typeof(GenTypes))]
    public static class AllSubclassesNonAbstract_Patch
    {
        internal static int lockTimeout = 100;
        private static ReaderWriterLock cacheLock = new();

        [HarmonyPrefix]
        [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
        [HarmonyPatch(nameof(GenTypes.AllSubclassesNonAbstract))]
        public static bool Prefix_Replace(Type baseType, ref List<Type> __result)
        {
            cacheLock.AcquireReaderLock(lockTimeout);
            if (GenTypes.cachedSubclassesNonAbstract.TryGetValue(baseType, out var typeList))
            {
                __result = typeList;
                cacheLock.ReleaseReaderLock();
            }
            else
            {
                cacheLock.UpgradeToWriterLock(lockTimeout);
                typeList = (from x in GenTypes.AllTypes.AsParallel()
                    where x.IsSubclassOf(baseType) && !x.IsAbstract
                    select x).ToList();
                GenTypes.cachedSubclassesNonAbstract.Add(baseType, typeList);
                cacheLock.ReleaseWriterLock();
            }
            return false;
        }
    }
}