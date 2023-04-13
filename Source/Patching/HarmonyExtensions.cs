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
        public static bool HasHarmonyPatchAll(this MemberInfo member)
        {
            return member.HasAttribute<HarmonyPatchAll>()
                || member.DeclaringType.HasAttribute<HarmonyPatchAll>();
        }

        public static string HarmonyPatchCategory(this MemberInfo member)
        {
            return member.GetCustomAttribute<PatchCategoryAttribute>(inherit: true)?.category
                ?? member.DeclaringType?.GetCustomAttribute<PatchCategoryAttribute>(inherit: true)?.category
                ?? RimThreadedHarmony.DefaultCategory;
        }

        public static Type HarmonyDeclaringType(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.declaringType)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.declaringType)
                ?? null;
        }

        public static string HarmonyMethodName(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.methodName)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.methodName)
                ?? null;
        }

        public static MethodType? HarmonyMethodType(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.methodType)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.methodType)
                ?? null;
        }

        public static Type[] HarmonyArgumentTypes(this MemberInfo member)
        {
            return HarmonyMergedValue(member, hm => hm.argumentTypes)
                ?? HarmonyMergedValue(member.DeclaringType, hm => hm.argumentTypes)
                ?? null;
        }

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

        public static MethodInfo ReversePatch(this (MethodBase, HarmonyMethod, MethodInfo) args)
        {
            var (original, standin, transpiler) = args;
            return Harmony.ReversePatch(original, standin, transpiler);
        }

        public static IEnumerable<MethodInfo> ReversePatch(this IEnumerable<(MethodBase, HarmonyMethod, MethodInfo)> argList)
        {
            foreach (var args in argList)
            {
                yield return args.ReversePatch();
            }
        }

        public static MethodInfo ReversePatch(this ReplacePatchSourceAttribute.Usage dele)
        {
            return dele.Invoke().ReversePatch();
        }

        public static IEnumerable<MethodInfo> ReversePatch(this ReplacePatchesSourceAttribute.Usage multiDele)
        {
            return multiDele.Invoke().ReversePatch();
        }
    }

#pragma warning disable 649

    [Serializable]
    class Replacements
    {
        public List<ClassReplacement> ClassReplacements;
    }

    [Serializable]
    class ClassReplacement
    {
        public string ClassName;
        public bool IgnoreMissing;
        public List<ThreadStaticDetail> ThreadStatics;
    }

    [Serializable]
    class ThreadStaticDetail
    {
        public string FieldName;
        public string PatchedClassName;
        public bool SelfInitialized;
    }

#pragma warning restore 649

}
