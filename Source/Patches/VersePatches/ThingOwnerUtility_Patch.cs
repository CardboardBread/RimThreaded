﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimThreaded.Patches.VersePatches;

public class ThingOwnerUtility_Patch
{
    //public static Dictionary<int, List<IThingHolder>> tmpHoldersDict = new Dictionary<int, List<IThingHolder>>();
    [ThreadStatic] public static Stack<IThingHolder> tmpStack;
    [ThreadStatic] public static List<IThingHolder> tmpHolders;
    [ThreadStatic] public static List<Thing> tmpThings;
    [ThreadStatic] public static List<IThingHolder> tmpMapChildHolders;

    internal static void RunDestructivePatches()
    {
        Type original = typeof(ThingOwnerUtility);
        Type patched = typeof(ThingOwnerUtility_Patch);
        RimThreadedHarmony.Prefix(original, patched, nameof(AppendThingHoldersFromThings));
        RimThreadedHarmony.Prefix(original, patched, nameof(GetAllThingsRecursively), new Type[] { typeof(IThingHolder), typeof(List<Thing>), typeof(bool), typeof(Predicate<IThingHolder>) });
        MethodInfo[] methods = original.GetMethods();
        MethodInfo GetAllThingsRecursivelyT = null;
        //MethodInfo originalPawnGetAllThings = original.GetMethod("GetAllThingsRecursively", bf, null, new Type[] { 
        //	typeof(Map), typeof(ThingRequest), typeof(List<Pawn>), typeof(bool), typeof(Predicate<IThingHolder>), typeof(bool) }, null);
        foreach (MethodInfo method in methods)
        {
            if (method.ToString().Equals("Void GetAllThingsRecursively[T](Verse.Map, Verse.ThingRequest, System.Collections.Generic.List`1[T], Boolean, System.Predicate`1[Verse.IThingHolder], Boolean)"))
            {
                GetAllThingsRecursivelyT = method;
                break;
            }
        }

        //MethodInfo originalPawnGetAllThings = methods[17];
        MethodInfo originalPawnGetAllThingsGeneric = GetAllThingsRecursivelyT.MakeGenericMethod(new Type[] { typeof(Pawn) });
        MethodInfo patchedPawnGetAllThings = patched.GetMethod(nameof(GetAllThingsRecursively_Pawn));
        HarmonyMethod prefixPawnGetAllThings = new HarmonyMethod(patchedPawnGetAllThings);
        RimThreadedHarmony.Harmony.Patch(originalPawnGetAllThingsGeneric, prefix: prefixPawnGetAllThings);

        //MethodInfo originalThingGetAllThings = methods[17];
        MethodInfo originalThingGetAllThingsGeneric = GetAllThingsRecursivelyT.MakeGenericMethod(new Type[] { typeof(Thing) });
        MethodInfo patchedThingGetAllThings = patched.GetMethod(nameof(GetAllThingsRecursively_Thing));
        HarmonyMethod prefixThingGetAllThings = new HarmonyMethod(patchedThingGetAllThings);
        RimThreadedHarmony.Harmony.Patch(originalThingGetAllThingsGeneric, prefix: prefixThingGetAllThings);

    }

    public static bool AppendThingHoldersFromThings(List<IThingHolder> outThingsHolders, IList<Thing> container)
    {
        if (container == null)
        {
            return false;
        }
        int i = 0;
        int count = container.Count;
        Thing thing;
        while (i < count)
        {
            try
            {
                thing = container[i];
            }
            catch (ArgumentOutOfRangeException) { break; }
            IThingHolder thingHolder = thing as IThingHolder;
            if (thingHolder != null)
            {
                lock (outThingsHolders)
                {
                    outThingsHolders.Add(thingHolder);
                }
            }
            ThingWithComps thingWithComps = container[i] as ThingWithComps;
            if (thingWithComps != null)
            {
                List<ThingComp> allComps = thingWithComps.AllComps;
                for (int j = 0; j < allComps.Count; j++)
                {
                    IThingHolder thingHolder2 = allComps[j] as IThingHolder;
                    if (thingHolder2 != null)
                    {
                        lock (outThingsHolders)
                        {
                            outThingsHolders.Add(thingHolder2);
                        }
                    }
                }
            }
            i++;
        }
        return false;
    }

    public static bool GetAllThingsRecursively(IThingHolder holder, List<Thing> outThings, bool allowUnreal = true, Predicate<IThingHolder> passCheck = null)
    {
        outThings.Clear();
        if (passCheck != null && !passCheck(holder))
        {
            return false;
        }
        //Stack<IThingHolder> tmpStack = new Stack<IThingHolder>();
        tmpStack.Push(holder);
        while (tmpStack.Count != 0)
        {
            IThingHolder thingHolder = tmpStack.Pop();
            if (allowUnreal || ThingOwnerUtility.AreImmediateContentsReal(thingHolder))
            {
                ThingOwner directlyHeldThings = thingHolder.GetDirectlyHeldThings();
                if (directlyHeldThings != null)
                {
                    outThings.AddRange(directlyHeldThings);
                }
            }
            //List<IThingHolder> tmpHolders = tmpHoldersDict[Thread.CurrentThread.ManagedThreadId];
            //List<IThingHolder> tmpHolders = new List<IThingHolder>();
            tmpHolders.Clear();
            thingHolder.GetChildHolders(tmpHolders);
            for (int i = 0; i < tmpHolders.Count; i++)
            {
                if (passCheck == null || passCheck(tmpHolders[i]))
                {
                    tmpStack.Push(tmpHolders[i]);
                }
            }
        }
        tmpStack.Clear();
        tmpHolders.Clear();
        return false;
    }
    public static bool GetAllThingsRecursively_Pawn(Map map,
        ThingRequest request, List<Pawn> outThings, bool allowUnreal = true,
        Predicate<IThingHolder> passCheck = null, bool alsoGetSpawnedThings = true)
    {
        lock (outThings)
        {
            outThings.Clear();
        }
        if (alsoGetSpawnedThings)
        {
            List<Thing> list = map.listerThings.ThingsMatching(request);
            for (int i = 0; i < list.Count; i++)
            {
                Pawn t = list[i] as Pawn;
                if (t != null)
                {
                    lock (outThings)
                    {
                        outThings.Add(t);
                    }
                }
            }
        }

        //List<IThingHolder> tmpMapChildHolders = new List<IThingHolder>();
        tmpMapChildHolders.Clear();
        map.GetChildHolders(tmpMapChildHolders);
        for (int j = 0; j < tmpMapChildHolders.Count; j++)
        {
            tmpThings.Clear();
            //List<Thing> tmpThings = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(tmpMapChildHolders[j], tmpThings, allowUnreal, passCheck);
            for (int k = 0; k < tmpThings.Count; k++)
            {
                Pawn t2 = tmpThings[k] as Pawn;
                if (t2 != null && request.Accepts(t2))
                {
                    lock (outThings)
                    {
                        outThings.Add(t2);
                    }
                }
            }
        }
        tmpThings.Clear();
        tmpMapChildHolders.Clear();
        return false;
    }

    public static bool GetAllThingsRecursively_Thing(Map map, ThingRequest request, List<Thing> outThings, bool allowUnreal = true, Predicate<IThingHolder> passCheck = null, bool alsoGetSpawnedThings = true)
    {
        outThings.Clear();
        if (alsoGetSpawnedThings)
        {
            List<Thing> list = map.listerThings.ThingsMatching(request);
            for (int i = 0; i < list.Count; i++)
            {
                Thing val = list[i];
                if (val != null)
                {
                    outThings.Add(val);
                }
            }
        }
        //List<IThingHolder> tmpMapChildHolders = new List<IThingHolder>();
        tmpMapChildHolders.Clear();
        map.GetChildHolders(tmpMapChildHolders);
        //List<Thing> tmpThings = new List<Thing>();
        for (int j = 0; j < tmpMapChildHolders.Count; j++)
        {
            tmpThings.Clear();
            GetAllThingsRecursively(tmpMapChildHolders[j], tmpThings, allowUnreal, passCheck);
            for (int k = 0; k < tmpThings.Count; k++)
            {
                if (tmpThings[k] is Thing val2 && request.Accepts(val2))
                {
                    outThings.Add(val2);
                }
            }
        }
        tmpThings.Clear();
        tmpMapChildHolders.Clear();
        return false;
    }
}