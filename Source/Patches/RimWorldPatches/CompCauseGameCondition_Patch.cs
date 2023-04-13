using System;
using System.Collections.Generic;
using HarmonyLib;
using RimThreaded.Utilities;
using RimWorld;
using Verse;

namespace RimThreaded.Patches.RimWorldPatches
{

    public class CompCauseGameCondition_Patch
    {
        [ThreadStatic] public static List<Map> tmpDeadConditionMaps;
        internal static void RunDestructivePatches()
        {
            Type original = typeof(CompCauseGameCondition);
            Type patched = typeof(CompCauseGameCondition_Patch);
            RimThreadedHarmony.Prefix(original, patched, nameof(GetConditionInstance));
            RimThreadedHarmony.Prefix(original, patched, nameof(CreateConditionOn));
            RimThreadedHarmony.Prefix(original, patched, nameof(CompTick));

            var oMethod = AccessTools.Method(original, nameof(CompCauseGameCondition.CompTick));
            var pMethod = AccessTools.Method(patched, nameof(CompCauseGameCondition.CompTick));
            var hpMethod = new HarmonyMethod(pMethod);
            RimThreadedHarmony.Harmony.Patch(original: oMethod, prefix: hpMethod);
        }

        public static void InitializeThreadStatics()
        {
            tmpDeadConditionMaps = new List<Map>();
        }

        [HarmonyPrefix]
        [DestructivePatch]
        [HarmonyPatch(typeof(CompCauseGameCondition), nameof(CompCauseGameCondition.GetConditionInstance))]
        public static bool GetConditionInstance(CompCauseGameCondition __instance, ref GameCondition __result, Map map)
        {
            if (!__instance.causedConditions.TryGetValue(map, out GameCondition value) && __instance.Props.preventConditionStacking)
            {
                value = map.GameConditionManager.GetActiveCondition(__instance.Props.conditionDef);
                if (value != null)
                {
                    lock (__instance)
                    {
                        __instance.causedConditions.Add(map, value);
                    }
                    __instance.SetupCondition(value, map);
                }
            }

            __result = value;
            return false;
        }

        public static bool CreateConditionOn(CompCauseGameCondition __instance, ref GameCondition __result, Map map)
        {
            GameCondition gameCondition = GameConditionMaker.MakeCondition(__instance.ConditionDef);
            gameCondition.Duration = gameCondition.TransitionTicks;
            gameCondition.conditionCauser = __instance.parent;
            map.gameConditionManager.RegisterCondition(gameCondition);
            lock (__instance)
            {
                __instance.causedConditions.Add(map, gameCondition);
            }
            __instance.SetupCondition(gameCondition, map);
            __result = gameCondition;
            return false;
        }

        public static bool CompTick(CompCauseGameCondition __instance)
        {
            if (__instance.Active)
            {
                foreach (Map map in Find.Maps)
                {
                    if (__instance.InAoE(map.Tile))
                    {
                        __instance.EnforceConditionOn(map);
                    }
                }
            }
            tmpDeadConditionMaps.Clear();

            foreach (KeyValuePair<Map, GameCondition> causedCondition in __instance.causedConditions)
            {
                if (causedCondition.Value.Expired || !causedCondition.Key.GameConditionManager.ConditionIsActive(causedCondition.Value.def))
                {
                    tmpDeadConditionMaps.Add(causedCondition.Key);
                }
            }

            foreach (Map tmpDeadConditionMap in tmpDeadConditionMaps)
            {
                if (!__instance.causedConditions.ContainsKey(tmpDeadConditionMap)) continue;
                lock (__instance)
                {
                    if (!__instance.causedConditions.ContainsKey(tmpDeadConditionMap)) continue;
                    Dictionary<Map, GameCondition> newCausedConditions = new Dictionary<Map, GameCondition>(__instance.causedConditions);
                    newCausedConditions.Remove(tmpDeadConditionMap);
                    __instance.causedConditions = newCausedConditions;
                }
            }
            return false;
        }

    }
}
