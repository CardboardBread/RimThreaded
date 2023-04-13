using HarmonyLib;
using System;
using System.Reflection;
using Verse;
using System.Collections.Generic;
using System.Reflection.Emit;
using static HarmonyLib.AccessTools;
using static RimThreaded.RimThreadedHarmony;
using System.Linq;
using UnityEngine;
using RimThreaded.Patches.UnityEnginePatches;

namespace RimThreaded.Patches.ModPatches
{
    [HarmonyPatch]
    public class AndroidTiers_Patch
    {
        public static Type PawnGroupMakerUtility_Patch = TypeByName("MOARANDROIDS.PawnGroupMakerUtility_Patch");
        public static Type GeneratePawns_Patch = PawnGroupMakerUtility_Patch?.GetNestedType("GeneratePawns_Patch");

        [HarmonyPrepare]
        public static bool Prepare(MethodBase original, Harmony harmony)
        {
            if (original == null && (PawnGroupMakerUtility_Patch == null || GeneratePawns_Patch == null))
            {
                return false;
            }

            return true;
        }

        public class GeneratePawns_Patch_Transpile
        {
            public static IEnumerable<CodeInstruction> Listener(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
            {
                List<CodeInstruction> l = instructions.ToList();
                bool match = false;


                //Replacement Instructions
                CodeInstruction loadToken = new CodeInstruction(OpCodes.Ldtoken, typeof(Texture2D).GetTypeInfo());
                CodeInstruction resolveToken = new CodeInstruction(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));


                for (int x = 0; x < l.Count; x++)
                {
                    CodeInstruction i = l[x];

                    if (i.opcode == OpCodes.Call
                        && (MethodInfo)i.operand == TargetMethodHelper())
                    {
                        match = true;

                        i.operand = typeof(Resources_Patch).GetMethod("Load");

                        l.Insert(x, resolveToken);
                        l.Insert(x, loadToken);


                    }
                    yield return l[x];
                }
                if (!match)
                {
                    Log.Error("No IL Instruction found for PawnGroupMakerUtility_Patch.");
                }
            }

            public static MethodBase TargetMethodHelper()
            {
                MethodInfo i = typeof(Resources).GetMethods().Single(
                    m =>
                        m.Name == "Load" &&
                        m.GetGenericArguments().Length == 1 &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string)
                    );


                return i.MakeGenericMethod(typeof(Texture2D));
            }
        }

        public static void Patch()
        {

            Type patched;
            if (GeneratePawns_Patch != null)
            {
                string methodName = "Listener";
                patched = typeof(GeneratePawns_Patch_Transpile);
                Log.Message("RimThreaded is patching " + GeneratePawns_Patch.FullName + " " + methodName);
                Log.Message("Utility_Patch::Listener != null: " + (Method(GeneratePawns_Patch, "Listener") != null));
                Log.Message("Utility_Patch_Transpile::Listener != null: " + (Method(patched, "Listener") != null));
                Transpile(GeneratePawns_Patch, patched, methodName);
            }
            Type androidTiers_Utils = TypeByName("MOARANDROIDS.Utils");
            if (androidTiers_Utils != null)
            {
                string methodName = nameof(getCachedCSM);
                Log.Message("RimThreaded is patching " + androidTiers_Utils.FullName + " " + methodName);
                Transpile(androidTiers_Utils, typeof(AndroidTiers_Patch), methodName);
            }
        }

        public static void set_Item(Dictionary<Thing, object> CSM, Thing t, object j)
        {
            lock (CSM)
            {
                CSM[t] = j;
            }
        }

        public static IEnumerable<CodeInstruction> getCachedCSM(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
        {
            Type ThingToCompSkyMind = typeof(Dictionary<,>).MakeGenericType(new[] { typeof(Thing), TypeByName("MOARANDROIDS.CompSkyMind") });
            foreach (CodeInstruction i in instructions)
            {
                if (i.opcode == OpCodes.Callvirt)
                {//CompSkyMind
                    if ((MethodInfo)i.operand == Method(ThingToCompSkyMind, "set_Item"))
                    {
                        i.operand = Method(typeof(AndroidTiers_Patch), nameof(set_Item));
                    }
                }
                yield return i;
            }
        }

    }
}
