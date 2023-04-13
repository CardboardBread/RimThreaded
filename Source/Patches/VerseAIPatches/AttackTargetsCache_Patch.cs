﻿using System;
using System.Collections.Generic;
using RimThreaded.Patching;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimThreaded.Patches.VerseAIPatches
{
    [HarmonyPatch(typeof(AttackTargetsCache))]
    public class AttackTargetsCache_Patch
    {
        private static readonly List<IAttackTarget> EmptyList = new List<IAttackTarget>();
        private static readonly HashSet<IAttackTarget> EmptySet = new HashSet<IAttackTarget>();

        private static readonly Dictionary<AttackTargetsCache, Dictionary<Faction, List<IAttackTarget>>> TargetsHostileToFactionDict =
            new Dictionary<AttackTargetsCache, Dictionary<Faction, List<IAttackTarget>>>();
        private static readonly Dictionary<AttackTargetsCache, List<Pawn>> PawnsInAggroMentalStateDict =
            new Dictionary<AttackTargetsCache, List<Pawn>>();
        private static readonly Dictionary<AttackTargetsCache, List<Pawn>> FactionlessHumanlikesDict =
            new Dictionary<AttackTargetsCache, List<Pawn>>();
        private static readonly Dictionary<AttackTargetsCache, List<IAttackTarget>> AllTargetsListDict =
            new Dictionary<AttackTargetsCache, List<IAttackTarget>>();

        [RebindFieldPatch]
        public static List<IAttackTarget> tmpTargets = new List<IAttackTarget>();

        [RebindFieldPatch]
        public static List<IAttackTarget> tmpToUpdate = new List<IAttackTarget>();

        public static void RunDestructivesPatches()
        {
            Type original = typeof(AttackTargetsCache);
            Type patched = typeof(AttackTargetsCache_Patch);
            RimThreadedHarmony.Prefix(original, patched, nameof(GetPotentialTargetsFor));
            RimThreadedHarmony.Prefix(original, patched, nameof(RegisterTarget));
            RimThreadedHarmony.Prefix(original, patched, nameof(DeregisterTarget));
            RimThreadedHarmony.Prefix(original, patched, nameof(TargetsHostileToFaction));
            RimThreadedHarmony.Prefix(original, patched, nameof(UpdateTarget));
        }

        public static bool DeregisterTarget(AttackTargetsCache __instance, IAttackTarget target)
        {
            if (!AllTargetsListDict.TryGetValue(__instance, out List<IAttackTarget> snapshotAllTargets)) return false;
            if (!snapshotAllTargets.Contains(target))
            {
                Log.Warning("Tried to deregister " + target + " but it's not in " + __instance.GetType());
                return false;
            }
            lock (AllTargetsListDict)
            {
                List<IAttackTarget> newAllTargets = new List<IAttackTarget>(snapshotAllTargets);
                newAllTargets.Remove(target);
                AllTargetsListDict[__instance] = newAllTargets;
            }

            Dictionary<Faction, List<IAttackTarget>> targetsHostileToFaction = getTargetsHostileToFactionList(__instance);
            List<Faction> allFactionsListForReading = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < allFactionsListForReading.Count; i++)
            {
                Faction faction = allFactionsListForReading[i];
                if (!targetsHostileToFaction.TryGetValue(faction, out List<IAttackTarget> hostileTargets)) continue;
                if (!hostileTargets.Contains(target)) continue;
                lock (targetsHostileToFaction)
                {
                    targetsHostileToFaction.TryGetValue(faction, out List<IAttackTarget> hostileTargets2);
                    if (hostileTargets2 == null || !hostileTargets2.Contains(target)) continue;
                    List<IAttackTarget> newHostileTargets = new List<IAttackTarget>(hostileTargets2);
                    newHostileTargets.Remove(target);
                    targetsHostileToFaction[faction] = newHostileTargets;
                }
            }

            if (!(target is Pawn pawn)) return false;
            lock (PawnsInAggroMentalStateDict)
            {
                if (PawnsInAggroMentalStateDict.TryGetValue(__instance, out List<Pawn> pawnsInAggroMentalStateList))
                {
                    if (pawnsInAggroMentalStateList.Contains(pawn))
                    {
                        List<Pawn> newPawnsInAggroMentalStateList = new List<Pawn>(pawnsInAggroMentalStateList);
                        newPawnsInAggroMentalStateList.Remove(pawn);
                        PawnsInAggroMentalStateDict[__instance] = newPawnsInAggroMentalStateList;
                    }
                }
            }
            lock (FactionlessHumanlikesDict)
            {
                if (!FactionlessHumanlikesDict.TryGetValue(__instance, out List<Pawn> factionlessHumanlikesList) ||
                    !factionlessHumanlikesList.Contains(pawn))
                    return false;
                List<Pawn> newFactionlessHumanlikesList = new List<Pawn>(factionlessHumanlikesList);
                newFactionlessHumanlikesList.Remove(pawn);
                FactionlessHumanlikesDict[__instance] = newFactionlessHumanlikesList;
            }

            return false;
        }

        public static bool RegisterTarget(AttackTargetsCache __instance, IAttackTarget target)
        {
            Thing thing = target.Thing;

            if (!thing.Spawned)
            {
                Log.Warning("Tried to register unspawned thing " + thing.ToStringSafe() + " in " + __instance.GetType());
                return false;
            }

            if (thing.Map != __instance.map)
            {
                Log.Warning("Tried to register attack target " + thing.ToStringSafe() + " but its Map is not this one.");
                return false;
            }

            lock (AllTargetsListDict)
            {
                if (AllTargetsListDict.TryGetValue(__instance, out List<IAttackTarget> snapshotAllTargets))
                {
                    if (snapshotAllTargets.Contains(target))
                    {
                        Log.Warning("Tried to register the same target twice " + target.ToStringSafe() + " in " + __instance.GetType());
                        return false;
                    }
                    snapshotAllTargets.Add(target);
                }
                else
                {
                    AllTargetsListDict[__instance] = new List<IAttackTarget>() { target };
                }

            }
            List<Faction> allFactionsListForReading = Find.FactionManager.AllFactionsListForReading;
            Dictionary<Faction, List<IAttackTarget>> targetsHostileToFaction = getTargetsHostileToFactionList(__instance);
            for (int i = 0; i < allFactionsListForReading.Count; i++)
            {
                Faction faction = allFactionsListForReading[i];
                if (!thing.HostileTo(faction)) continue;
                lock (targetsHostileToFaction)
                {
                    if (targetsHostileToFaction.TryGetValue(faction, out List<IAttackTarget> hostileTargets))
                    {
                        targetsHostileToFaction[faction] = new List<IAttackTarget>(hostileTargets) { target };
                    }
                    else
                    {
                        targetsHostileToFaction[faction] = new List<IAttackTarget>() { target };
                    }
                }
            }

            if (!(target is Pawn pawn)) return false;
            if (pawn.InAggroMentalState)
            {
                lock (PawnsInAggroMentalStateDict)
                {
                    if (PawnsInAggroMentalStateDict.TryGetValue(__instance, out List<Pawn> pawnsInAggroMentalStateList))
                    {
                        PawnsInAggroMentalStateDict[__instance] = new List<Pawn>(pawnsInAggroMentalStateList) { pawn };
                    }
                    else
                    {
                        PawnsInAggroMentalStateDict[__instance] = new List<Pawn>() { pawn };
                    }
                }
            }

            if (pawn.Faction != null || !pawn.RaceProps.Humanlike) return false;
            lock (FactionlessHumanlikesDict)
            {
                if (FactionlessHumanlikesDict.TryGetValue(__instance, out List<Pawn> factionlessHumanlikesList))
                {
                    FactionlessHumanlikesDict[__instance] = new List<Pawn>(factionlessHumanlikesList) { pawn };
                }
                else
                {
                    FactionlessHumanlikesDict[__instance] = new List<Pawn>() { pawn };
                }
            }

            return false;

        }

        public static bool UpdateTarget(AttackTargetsCache __instance, IAttackTarget t)
        {
            if (!getAllTargets(__instance).Contains(t)) return false;
            DeregisterTarget(__instance, t);
            Thing thing = t.Thing;
            if (thing.Spawned && thing.Map == __instance.map)
            {
                RegisterTarget(__instance, t);
            }
            return false;
        }

        public static bool GetPotentialTargetsFor(AttackTargetsCache __instance, ref List<IAttackTarget> __result, IAttackTargetSearcher th)
        {
            Thing thing = th.Thing;
            List<IAttackTarget> targets = new List<IAttackTarget>();
            Faction faction = thing.Faction;
            if (faction != null)
            {
                List<IAttackTarget> snapshotTargetsHostileToFactionList = getTargetsHostileToFactionList(__instance, faction);
                foreach (IAttackTarget item in snapshotTargetsHostileToFactionList)
                {
                    if (thing.HostileTo(item.Thing))
                    {
                        targets.Add(item);
                    }
                }
            }
            if (PawnsInAggroMentalStateDict.TryGetValue(__instance, out List<Pawn> listPawnsInAggroMentalState))
            {
                foreach (Pawn pawn in listPawnsInAggroMentalState)
                {
                    if (thing.HostileTo(pawn))
                    {
                        targets.Add(pawn);
                    }
                }
            }

            if (FactionlessHumanlikesDict.TryGetValue(__instance, out List<Pawn> listFactionlessHumanlikes))
            {
                foreach (Pawn pawn2 in listFactionlessHumanlikes)
                {
                    if (thing.HostileTo(pawn2))
                    {
                        targets.Add(pawn2);
                    }
                }
            }

            if (th is Pawn pawn3 && PrisonBreakUtility.IsPrisonBreaking(pawn3))
            {
                Faction hostFaction = pawn3.guest.HostFaction;
                List<Pawn> list = __instance.map.mapPawns.SpawnedPawnsInFaction(hostFaction);
                for (int i = 0; i < list.Count; i++)
                {
                    if (thing.HostileTo(list[i]))
                    {
                        targets.Add(list[i]);
                    }
                }
            }
            __result = targets;
            return false;
        }

        public static bool TargetsHostileToFaction(AttackTargetsCache __instance, ref HashSet<IAttackTarget> __result, Faction f)
        {
            if (f == null)
            {
                Log.Warning("Called TargetsHostileToFaction with null faction.");
                __result = EmptySet;
                return false;
            }

            __result = new HashSet<IAttackTarget>(getTargetsHostileToFactionList(__instance, f));
            return false;
        }

        private static List<IAttackTarget> getTargetsHostileToFactionList(AttackTargetsCache __instance, Faction faction)
        {
            if (faction == null)
            {
                Log.Warning("Called getTargetsHostileToFactionList with null faction.");
            }
            else
            {
                if (getTargetsHostileToFactionList(__instance).TryGetValue(faction, out List<IAttackTarget> listIAttackTargets))
                {
                    return listIAttackTargets;
                }
            }
            return EmptyList;
        }
        private static Dictionary<Faction, List<IAttackTarget>> getTargetsHostileToFactionList(AttackTargetsCache __instance)
        {
            if (TargetsHostileToFactionDict.TryGetValue(__instance,
                out Dictionary<Faction, List<IAttackTarget>> factionIAttackTargetDict)) return factionIAttackTargetDict;
            lock (TargetsHostileToFactionDict)
            {
                if (!TargetsHostileToFactionDict.TryGetValue(__instance, out Dictionary<Faction, List<IAttackTarget>> factionIAttackTargetDict2))
                {
                    factionIAttackTargetDict = new Dictionary<Faction, List<IAttackTarget>>();
                    TargetsHostileToFactionDict[__instance] = factionIAttackTargetDict;
                }
                else
                {
                    factionIAttackTargetDict = factionIAttackTargetDict2;
                }
            }
            return factionIAttackTargetDict;
        }

        private static List<IAttackTarget> getAllTargets(AttackTargetsCache __instance)
        {
            return AllTargetsListDict.TryGetValue(__instance, out List<IAttackTarget> allTargetsList) ? allTargetsList : EmptyList;
        }

    }
}
