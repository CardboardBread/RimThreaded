using HarmonyLib;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.StaticReplacement
{
    // An attribute to declare a field should be replaced with a dynamically created or referenced premade version.
    // Operates independent of location, but can grab info from nearby harmony attributes (same member or declaring type).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
    public class ReplaceFieldAttribute : DoubleTargetPatchAttribute
    {
        public static IEnumerable<ReplaceFieldAttribute> GetLocalUsages() => AttributeUtility.GetLocalUsages<ReplaceFieldAttribute>();

        public bool SelfInitialized { get; set; } = true;
        public Type InitializerType { get; set; } = null;
        public string InitializerName { get; set; } = null;

        internal new FieldInfo _source;
        internal new FieldInfo _target;

        public ReplaceFieldAttribute()
        {
        }

        public ReplaceFieldAttribute(Type targetType, string targetName)
        {
            TargetType = targetType;
            TargetName = targetName;
        }

        public ReplaceFieldAttribute(Type sourceType, string sourceName, Type targetType, string targetName) : this (targetType, targetName)
        {
            SourceType = sourceType;
            SourceName = sourceName;
        }

        internal override void Locate(HarmonyMethod nearby)
        {
            _source ??= AccessTools.DeclaredField(SourceType, SourceName);
            _target ??= AccessTools.DeclaredField(TargetType, TargetName);
        }
    }
}
