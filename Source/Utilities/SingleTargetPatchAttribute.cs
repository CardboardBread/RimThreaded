using HarmonyLib;
using RimThreaded.StaticReplacement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Utilities
{
    // Base pattern for harmony-style attributes that target some named member, and perform an operation between the
    // named member and the attribute's declaring member.
    public abstract class SingleTargetPatchAttribute : Attribute, ILocationAware
    {
        public Type DeclaringType { get; set; }
        public string MemberName { get; set; }

        internal MemberInfo _target;
        internal MemberInfo _parent;

        void ILocationAware.Locate(MemberInfo member)
        {
            _parent = member ?? throw new ArgumentNullException(nameof(member));
            var neighbours = HarmonyMethodExtensions.GetFromType(member as Type ?? member.DeclaringType);
            var harmony = HarmonyMethod.Merge(neighbours);
            DeclaringType ??= harmony.declaringType ?? throw new ArgumentNullException(nameof(DeclaringType));
            MemberName ??= harmony.methodName ?? member.Name ?? throw new ArgumentNullException(nameof(MemberName));
            Locate(harmony);
        }

        internal abstract void Locate(HarmonyMethod nearby);
    }
}
