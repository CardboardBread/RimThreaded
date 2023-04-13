using HarmonyLib;
using MonoMod.Utils;
using RimThreaded.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patching
{
    // HarmonyPatchCategory, but ony any harmony patch target, such that a single patch class can have multiple categories.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Method, AllowMultiple = false)]
    public class PatchCategoryAttribute : Attribute
    {
        public readonly string category;

        public PatchCategoryAttribute(string category = null)
        {
            this.category = category;
        }
    }

    // Singular marker attribute like ReplacePatchesSourceAttribute
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ReplacePatchSourceAttribute : Attribute, ILocationAware
    {
        public delegate (MethodBase, MethodBase) Usage();

        internal MethodInfo _method;
        internal Usage _delegate;

        public void Locate(MemberInfo member)
        {
            _method = (MethodInfo)member;
            _delegate = ((MethodInfo)member).CreateDelegate<Usage>();
        }
    }

    // Marker attribute to declare methods that return data that is applicable for non-attribute replacement patching.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ReplacePatchesSourceAttribute : Attribute, ILocationAware
    {
        public delegate IEnumerable<(MethodBase, MethodBase)> Usage();

        internal MethodInfo _method;
        internal Usage _delegate;

        public void Locate(MemberInfo member)
        {
            if (false) throw new ArgumentException($"{typeof(ReplacePatchesSourceAttribute)} usage member must be static");
            _method = (MethodInfo)member;
            _delegate = ((MethodInfo)member).CreateDelegate<Usage>();
        }
    }

    // Marker attribute of initialization methods for 'premade' static member replacement.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ThreadStaticInitializerAttribute : Attribute, ILocationAware
    {
        public delegate void Usage();

        internal MethodInfo _method;
        internal Usage _delegate;

        public void Locate(MemberInfo member)
        {
            _method = (MethodInfo)member;
            _delegate = ((MethodInfo)member).CreateDelegate<Usage>();
        }
    }
}
