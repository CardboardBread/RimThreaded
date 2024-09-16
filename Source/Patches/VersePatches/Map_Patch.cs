using HarmonyLib;
using RimThreaded.Patching;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Threading;
using Verse;

namespace RimThreaded.Patches.VersePatches;

[HarmonyPatch(typeof(Map))]
public static class Map_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(Map.IsPlayerHome), MethodType.Getter)]
    public static bool IsPlayerHome_Replace(Map __instance, ref MapInfo ___info, ref bool __result)
    {
        __result = Method(__instance, ___info);
        return false;

        static bool Method(Map @this, MapInfo info)
        {
            if (info != null && info.parent != null && info.parent.def != null && info.parent.def.canBePlayerHome)
            {
                return info.parent.Faction == Faction.OfPlayer;
            }

            return false;
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(Map.MapPreTick))]
    public static bool MapPreTick_Replace(Map __instance)
    {
        RimThreaded.Invoke(__instance.itemAvailability.Tick,
            __instance.listerHaulables.ListerHaulablesTick,
            __instance.autoBuildRoofAreaSetter.AutoBuildRoofAreaSetterTick_First,
            __instance.roofCollapseBufferResolver.CollapseRoofsMarkedToCollapse,
            __instance.windManager.WindManagerTick,
            __instance.mapTemperature.MapTemperatureTick,
            __instance.temporaryThingDrawer.Tick);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(Map.MapPostTick))]
    public static bool MapPostTick_Replace(Map __instance)
    {
        RimThreaded.Invoke(__instance.wildAnimalSpawner.WildAnimalSpawnerTick,
            __instance.wildPlantSpawner.WildPlantSpawnerTick,
            __instance.powerNetManager.PowerNetsTick,
            __instance.steadyEnvironmentEffects.SteadyEnvironmentEffectsTick,
            __instance.gasGrid.Tick,
            ModsConfig.BiotechActive ? __instance.pollutionGrid.PollutionTick : Nothing,
            __instance.lordManager.LordManagerTick,
            __instance.passingShipManager.PassingShipManagerTick,
            __instance.debugDrawer.DebugDrawerTick,
            __instance.lordsStarter.VoluntarilyJoinableLordsStarterTick,
            __instance.gameConditionManager.GameConditionManagerTick,
            __instance.weatherManager.WeatherManagerTick,
            __instance.resourceCounter.ResourceCounterTick,
            __instance.weatherDecider.WeatherDeciderTick,
            __instance.fireWatcher.FireWatcherTick,
            __instance.flecks.FleckManagerTick,
            __instance.effecterMaintainer.EffecterMaintainerTick,
            () => MapComponentUtility.MapComponentTick(__instance));
        return false;
        static void Nothing() { }
    }

#if false
        public static void MapsPostTickPrepare()
        {
            SteadyEnvironmentEffects_Patch.totalSteadyEnvironmentEffectsTicks = 0;
            SteadyEnvironmentEffects_Patch.steadyEnvironmentEffectsTicksCompleted = 0;
            SteadyEnvironmentEffects_Patch.steadyEnvironmentEffectsCount = 0;
            WildPlantSpawner_Patch.wildPlantSpawnerCount = 0;
            WildPlantSpawner_Patch.wildPlantSpawnerTicksCount = 0;
            WildPlantSpawner_Patch.wildPlantSpawnerTicksCompleted = 0;
            TradeShip_Patch.totalTradeShipTicks = 0;
            TradeShip_Patch.totalTradeShipTicksCompleted = 0;
            TradeShip_Patch.totalTradeShipsCount = 0;
            try
            {
                List<Map> maps = Find.Maps;
                for (int j = 0; j < maps.Count; j++)
                {
                    Map map = maps[j];
                    map.MapPostTick();
                }
            }
            catch (Exception ex3)
            {
                Log.Error(ex3.ToString());
            }

        }

        public static void MapPostListTick()
        {
            SteadyEnvironmentEffects_Patch.SteadyEffectTick();
            WildPlantSpawner_Patch.WildPlantSpawnerListTick();
            TradeShip_Patch.PassingShipListTick();
        } 
#endif
}