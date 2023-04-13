using RimThreaded.Utilities;
using System;
using System.Reflection;
using Verse;

namespace RimThreaded.Patching
{
    // Base pattern for harmony-adjacent attributes that target two named members and perform an operation between
    // both members. A location-agnostic version of SingleTargetPatchAttribute.
    // Will attempt to find source member info from nearby harmony attributes.
    public abstract class DoubleTargetPatchAttribute : Attribute, ILocationAware
    {
        public Type TargetType { get; set; }
        public string TargetName { get; set; }
        public Type SourceType { get; set; }
        public string SourceName { get; set; }

        internal MemberInfo Parent { get; set; }
        internal MemberInfo Target { get; set; }
        internal MemberInfo Source { get; set; }

        public bool IsIncompatibleTarget() => TargetType != null && TargetName != null && Target == null;

        public bool IsIncompatibleSource() => SourceType != null && SourceName != null && Source == null;

        public virtual void Locate(MemberInfo member)
        {
            Parent = member ?? throw new ArgumentNullException(nameof(member));

            TargetType ??= member.HarmonyDeclaringType() ?? null;
            TargetName ??= member.HarmonyMethodName() ?? member.Name ?? null;
            Target = LocateTarget(member);

            SourceType ??= member.DeclaringType ?? null;
            SourceName ??= member.HarmonyMethodName() ?? member.Name ?? null;
            Source = LocateSource(member);
        }

        protected abstract MemberInfo LocateTarget(MemberInfo member);

        protected abstract MemberInfo LocateSource(MemberInfo member);

        public bool IsLocated() => Parent != null;
    }

    public abstract class DoubleTargetPatchAttribute<T, S> : DoubleTargetPatchAttribute where T : MemberInfo where S : MemberInfo
    {
        internal virtual new T Target { get => base.Target as T; set => base.Target = value; }

        internal virtual new S Source { get => base.Source as S; set => base.Source = value; }
    }
}
