using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimThreaded.Utilities;
using RimWorld;
using Verse;

namespace RimThreaded.RW_Patches
{
    [ReplaceField<ThreadStaticAttribute>(typeof(IdeoManager), nameof(IdeoManager.activeRitualsTmp))]
    [RequireLock(typeof(IdeoManager))]
    [HarmonyPatch(typeof(IdeoManager))]
    class IdeoManager_Patch
    {
        internal static void RunNonDestructivePatches()//there may be the need for locks in the IdeoManager
        {
            Type original = typeof(IdeoManager);
        }

        public static List<Ideo> Ideos;
        public static int IdeosCount;

        public static void IdeosPrepare()
        {
            Ideos = Current.Game.World.ideoManager.ideos;
            IdeosCount = Ideos.Count;
        }

        public static void IdeosTick()
        {
            while (true)
            {
                int index = Interlocked.Decrement(ref IdeosCount);
                if (index < 0) return;
                try
                {
                    Ideos[index].IdeoTick();
                }
                catch (Exception e)
                {
                    Log.Error("Exception ticking Ideo: " + Ideos[index].ToString() + ": " + e);
                }
            }
        }

        [HarmonyReversePatch]
        [DestructivePatch]
        [HarmonyPatch(typeof(IdeoManager), nameof(IdeoManager.IdeoManagerTick))]
        public static void IdeoManagerTick(IdeoManager __instance, ref List<Ideo> ___ideos, ref List<Ideo> ___toRemove)
        {
            GenAsync.InlineForEach(___ideos, i => i.IdeoTick());
            GenAsync.InlineForEach(___toRemove, i => __instance.Remove(i));
            ___toRemove.Clear();
        }
    }
}
