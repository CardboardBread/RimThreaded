using HarmonyLib;
using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RimThreaded.Patching
{
    public static class HarmonyExtensions
    {
        /// <summary>
        /// Determines if a member posesses the HarmonyPatchAll attribute, which may be inherited from the member's declaring type.
        /// </summary>
        public static bool HasHarmonyPatchAll(this MemberInfo member)
        {
            return member.HasAttribute<HarmonyPatchAll>()
                || member.DeclaringType.HasAttribute<HarmonyPatchAll>();
        }

        /// <summary>
        /// Gets the Harmony Patch Category for a Harmony patch declared on this member, which may be inherited from the member's declaring type.
        /// </summary>
        public static string HarmonyPatchCategory(this MemberInfo member)
        {
            return member.GetCustomAttribute<PatchCategoryAttribute>(inherit: true)?.category
                ?? member.DeclaringType?.GetCustomAttribute<PatchCategoryAttribute>(inherit: true)?.category
                ?? RimThreadedHarmony.DefaultCategory;
        }

        /// <summary>
        /// Gets the Harmony Declaring Type for a Harmony patch declared on this member, this may be inherited from the member's declaring type.
        /// </summary>
        public static Type HarmonyDeclaringType(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.declaringType)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.declaringType)
                ?? null;
        }

        /// <summary>
        /// Gets the name of the method(s) targeted by a Harmony patch declared on this member, this may be inherited from the member's declaring type.
        /// </summary>
        public static string HarmonyMethodName(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.methodName)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.methodName)
                ?? null;
        }

        /// <summary>
        /// Gets the Harmony-specific category for the method targeted by a Harmony patch declared on this member, this may be inherited from the member's declaring type.
        /// </summary>
        public static MethodType? HarmonyMethodType(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.methodType)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.methodType)
                ?? null;
        }

        /// <summary>
        /// Gets the Harmony-specific variation in the argument typing for the method targeted by a Harmony patch declared on this member, this may be inherited from the member's declaring type.
        /// </summary>
        public static Type[] HarmonyArgumentTypes(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.argumentTypes)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.argumentTypes)
                ?? null;
        }

        /// <summary>
        /// Gets the priority of the Harmony patch declared on this member, this may be inherited from the member's declaring type.
        /// </summary>
        public static int? HarmonyPriority(this MemberInfo member)
        {
            static int? getter(HarmonyMethod hm) => hm.priority >= 0 ? hm.priority : null;
            return HarmonyMergedValue(member, getter)
                ?? HarmonyMergedValue(member.DeclaringType, getter)
                ?? null;
        }

        public static T HarmonyField<T>(this MemberInfo member, Func<HarmonyMethod, T> getter)
        {
            return HarmonyMergedValue(member, getter)
                ?? HarmonyMergedValue(member.DeclaringType, getter)
                ?? default;
        }

        private static T HarmonyMergedValue<T>(this MemberInfo member, Func<HarmonyMethod, T> getter)
        {
            if (member is null || getter is null) return default;
            return member.GetCustomAttributes<HarmonyAttribute>(inherit: true)
                .Where(a => a.info != null)
                .Select(a => getter.Invoke(a.info))
                .FirstOrDefault(res => res != null);
        }
    }
}
