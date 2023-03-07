using HarmonyLib;
using System;
using System.Reflection;

namespace RimThreaded.Utilities
{
    // Base pattern for harmony-adjacent attributes that target two named members and perform an operation between
    // both members. A location-agnostic version of SingleTargetPatchAttribute.
    // Will attempt to find source member info from nearby harmony attributes.
    public abstract class DoubleTargetPatchAttribute : Attribute, ILocationAware
    {
        public Type SourceType { get; set; }
        public string SourceName { get; set; }
        public Type TargetType { get; set; }
        public string TargetName { get; set; }

        internal MemberInfo _parent;
        internal MemberInfo _source;
        internal MemberInfo _target;

        void ILocationAware.Locate(MemberInfo member)
        {
            _parent = member ?? throw new ArgumentNullException(nameof(member));
            var neighbours = HarmonyMethodExtensions.GetFromType(member as Type ?? member.DeclaringType);
            var harmony = HarmonyMethod.Merge(neighbours);
            SourceType ??= harmony.declaringType ?? throw new ArgumentNullException(nameof(SourceType));
            SourceName ??= harmony.methodName ?? member.Name ?? throw new ArgumentNullException(nameof(SourceName));
            TargetType ??= member.DeclaringType ?? throw new ArgumentNullException(nameof(TargetType));
            TargetName ??= harmony.methodName ?? member.Name ?? throw new ArgumentNullException(nameof(TargetName));
            Locate(harmony);
        }

        internal abstract void Locate(HarmonyMethod nearby);
    }
}
