using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace RimThreaded.Patches;

// Harmony patch class to rebind and box fields with weak references, intended for fields that hold/waste a lot of data.
[HarmonyPatch]
public static class Patch_WeakReference
{
    // TODO: rebind fields but streamlined for only boxing with WeakReference.
}