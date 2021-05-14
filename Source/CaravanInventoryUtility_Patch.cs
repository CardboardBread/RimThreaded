﻿using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimThreaded
{
    class CaravanInventoryUtility_Patch
    {
        [ThreadStatic] public static List<Thing> inventoryItems = new List<Thing>();
        [ThreadStatic] public static List<Thing> inventoryToMove = new List<Thing>();
        [ThreadStatic] public static List<Apparel> tmpApparel = new List<Apparel>();
        [ThreadStatic] public static List<ThingWithComps> tmpEquipment = new List<ThingWithComps>();
        public static void InitializeThreadStatics()
        {
            inventoryItems = new List<Thing>();
            inventoryToMove = new List<Thing>();
            tmpApparel = new List<Apparel>();
            tmpEquipment = new List<ThingWithComps>();
        }

        internal static void RunNonDestructivePatches()
        {
            Type original = typeof(CaravanInventoryUtility);
            Type patched = typeof(CaravanInventoryUtility_Patch);
            RimThreadedHarmony.AddAllMatchingFields(original, patched);
            RimThreadedHarmony.TranspileFieldReplacements(original, "AllInventoryItems");
            RimThreadedHarmony.TranspileFieldReplacements(original, "CaravanInventoryUtilityStaticUpdate");
            RimThreadedHarmony.TranspileFieldReplacements(original, "MoveAllInventoryToSomeoneElse");
            RimThreadedHarmony.TranspileFieldReplacements(original, "MoveAllApparelToSomeonesInventory");
            RimThreadedHarmony.TranspileFieldReplacements(original, "MoveAllEquipmentToSomeonesInventory");
        }
    }
}
