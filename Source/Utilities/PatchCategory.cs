using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    // HarmonyPatchCategory, but ony any harmony patch target, such that a single patch class can have multiple categories.
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Method, AllowMultiple = false)]
    public class PatchCategory : Attribute
    {
        public readonly string category;

        public PatchCategory(string category = null)
        {
            this.category = category;
        }
    }
}
