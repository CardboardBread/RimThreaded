using System;
using RimThreaded.Patching;
using UnityEngine;
using Verse.Sound;
using static System.Threading.Thread;
using static RimThreaded.RimThreaded;

namespace RimThreaded.Patches.VerseSoundPatches
{
    [HarmonyPatch(typeof(AudioSourceMaker))]
    public class AudioSourceMaker_Patch
    {
        static readonly Func<object[], object> safeFunction = parameters =>
            AudioSourceMaker.NewAudioSourceOn((GameObject)parameters[0]);

        [HarmonyPrefix]
        [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
        [HarmonyPatch(nameof(AudioSourceMaker.NewAudioSourceOn))]
        public static bool NewAudioSourceOn(ref AudioSource __result, GameObject go)
        {
            if (!CurrentThread.IsBackground || !allWorkerThreads.TryGetValue(CurrentThread, out ThreadState threadInfo))
                return true;
            threadInfo.safeFunctionRequest = new object[] { safeFunction, new object[] { go } };
            MainWaitHandle.Set();
            threadInfo.eventWaitStart.WaitOne();
            __result = (AudioSource)threadInfo.safeFunctionResult;
            return false;
        }

    }
}
