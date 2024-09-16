using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patches.VersePatches;

[HarmonyPatch(typeof(AutoSlaughterManager))]
public static class AutoSlaughterManager_Patch
{
    [RebindFieldPatch]
    public static List<Pawn> tmpAnimals = new List<Pawn>();

    [RebindFieldPatch]
    public static List<Pawn> tmpAnimalsMale = new List<Pawn>();

    [RebindFieldPatch]
    public static List<Pawn> tmpAnimalsMaleYoung = new List<Pawn>();

    [RebindFieldPatch]
    public static List<Pawn> tmpAnimalsFemale = new List<Pawn>();

    [RebindFieldPatch]
    public static List<Pawn> tmpAnimalsFemaleYoung = new List<Pawn>();

    [RebindFieldPatch]
    public static List<Pawn> tmpAnimalsPregnant = new List<Pawn>();
}