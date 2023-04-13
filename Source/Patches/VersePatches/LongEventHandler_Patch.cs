using System;
using Verse;
using System.Collections.Concurrent;
using RimThreaded.Patching;
using HarmonyLib;

namespace RimThreaded.Patches.VersePatches
{
    [HarmonyPatch(typeof(LongEventHandler))]
    public class LongEventHandler_Patch
    {
        public static ConcurrentQueue<Action> toExecuteWhenFinished2 = new();

        [PatchCategory("Destructive")]
        [HarmonyPrefix]
        [HarmonyPatch(nameof(LongEventHandler.ExecuteToExecuteWhenFinished))]
        public static bool ExecuteToExecuteWhenFinished()
        {
            if (toExecuteWhenFinished2.Count > 0)
            {
                DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
            }
            while (toExecuteWhenFinished2.TryDequeue(out Action action))
            {
                DeepProfiler.Start(action.Method.DeclaringType + " -> " + action.Method);
                try
                {
                    action();
                }
                catch (Exception arg)
                {
                    Log.Error("Could not execute post-long-event action. Exception: " + arg);
                }
                finally
                {
                    DeepProfiler.End();
                }
            }

            if (toExecuteWhenFinished2.Count > 0)
            {
                DeepProfiler.End();
            }

            LongEventHandler.toExecuteWhenFinished.Clear();
            return false;
        }

        [PatchCategory("Destructive")]
        [HarmonyPrefix]
        [HarmonyPatch(nameof(LongEventHandler.ExecuteWhenFinished))]
        public static bool ExecuteWhenFinished(Action action)
        {
            toExecuteWhenFinished2.Enqueue(action);
            return true;
        }

        [PatchCategory("NonDestructive")]
        [HarmonyPrefix]
        [HarmonyPatch(nameof(LongEventHandler.RunEventFromAnotherThread))]
        public static bool RunEventFromAnotherThread(Action action)
        {
            RimThreaded.InitializeAllThreadStatics();
            return true;
        }

    }



}
