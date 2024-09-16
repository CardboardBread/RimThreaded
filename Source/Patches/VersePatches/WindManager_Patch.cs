using HarmonyLib;
using RimThreaded.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Patches.VersePatches;

[HarmonyPatch(typeof(WindManager))]
public class WindManager_Patch
{
    [HarmonyPostfix]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(WindManager.WindManagerTick))]
    public static void Postfix_WindManagerTick(WindManager __instance)
    {
        if (Find.CurrentMap == __instance.map)
        {
            Parallel.ForEach(WindManager.plantMaterials, mat => mat.SetFloat(ShaderPropertyIDs.SwayHead, __instance.plantSwayHead));
        }
    }

    internal static MethodInfo _findCurrentMap = AccessTools.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
    internal static FieldInfo _windManagerMap = AccessTools.Field(typeof(WindManager), nameof(WindManager.map));

    // Change the if block at the end of `WindManager.WindManagerTick` to do nothing.
    // We then use `Postfix_WindManagerTick` to replace `plantMaterials[j].SetFloat(ShaderPropertyIDs.SwayHead, plantSwayHead)`
    [HarmonyTranspiler]
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch(nameof(WindManager.WindManagerTick))]
    public static IEnumerable<CodeInstruction> Transpile_WindManagerTick(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
    {
        var list = instructions.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (i + 3 < list.Count
                && list[i].Calls(_findCurrentMap)           // IL_0109: call      class Verse.Map Verse.Find::get_CurrentMap()
                && list[i + 1].IsLdarg(0)                   // IL_010E: ldarg.0
                && list[i + 2].LoadsField(_windManagerMap)  // IL_010F: ldfld     class Verse.Map Verse.WindManager::map
                && list[i + 3].opcode == OpCodes.Bne_Un_S)  // IL_0114: bne.un.s  IL_014B      
            {
                list[i + 3].operand = OpCodes.Br; // Replace not equal jump with regular jump, if block now just returns.
            }
        }
        return list;
    }
}