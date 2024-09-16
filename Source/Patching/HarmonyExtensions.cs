using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RimThreaded.Patching;

public static class HarmonyExtensions
{
    /// <summary>
    /// Determines if a member posesses the HarmonyPatchAll attribute, which may be inherited from the member's declaring type.
    /// </summary>
    public static bool HasHarmonyPatchAll(this MemberInfo member)
    {
        return member.GetCustomAttribute<HarmonyPatchAll>(inherit: true) is not null
               || member.DeclaringType?.GetCustomAttribute<HarmonyPatchAll>(inherit: true) is not null;
    }

    /// <summary>
    /// Gets the Harmony Patch Category for a Harmony patch declared on this member, which may be inherited from the member's declaring type.
    /// </summary>
    public static string HarmonyPatchCategory(this MemberInfo member)
    {
        return member.GetCustomAttribute<PatchCategoryAttribute>(inherit: true)?.Category
               ?? member.DeclaringType?.GetCustomAttribute<PatchCategoryAttribute>(inherit: true)?.Category
               ?? RimThreadedHarmony.DefaultCategory;
    }

    /// <summary>
    /// Gets the Harmony Declaring Type for a Harmony patch declared on this member, this may be inherited from the member's declaring type.
    /// </summary>
    public static Type HarmonyDeclaringType(this MemberInfo member)
    {
        return InheritedHarmonyMergedValue(member, hm => hm.declaringType);
    }

    /// <summary>
    /// Gets the name of the method(s) targeted by a Harmony patch declared on this member, this may be inherited from the member's declaring type.
    /// </summary>
    public static string HarmonyMethodName(this MemberInfo member)
    {
        return InheritedHarmonyMergedValue(member, hm => hm.methodName);
    }

    /// <summary>
    /// Gets the Harmony-specific category for the method targeted by a Harmony patch declared on this member, this may be inherited from the member's declaring type.
    /// </summary>
    public static MethodType? HarmonyMethodType(this MemberInfo member)
    {
        return InheritedHarmonyMergedValue(member, hm => hm.methodType);
    }

    /// <summary>
    /// Gets the Harmony-specific variation in the argument typing for the method targeted by a Harmony patch declared on this member, this may be inherited from the member's declaring type.
    /// </summary>
    public static Type[] HarmonyArgumentTypes(this MemberInfo member)
    {
        return InheritedHarmonyMergedValue(member, hm => hm.argumentTypes);
    }

    /// <summary>
    /// Gets the priority of the Harmony patch declared on this member, this may be inherited from the member's declaring type.
    /// </summary>
    public static int? HarmonyPriority(this MemberInfo member)
    {
        static int? Getter(HarmonyMethod hm) => hm.priority >= 0 ? hm.priority : null;
        return InheritedHarmonyMergedValue(member, Getter);
    }

    /// <summary>
    /// Get a specific value from Harmony Attributes on the given member or its parent.
    /// </summary>
    private static T InheritedHarmonyMergedValue<T>(this MemberInfo member, Func<HarmonyMethod, T> getter)
    {
        return HarmonyMergedValue(member, getter)
               ?? HarmonyMergedValue(member.DeclaringType, getter)
               ?? default;
    }

    /// <summary>
    /// Get a specific value from Harmony Attributes on the given member.
    /// </summary>
    private static T HarmonyMergedValue<T>(this MemberInfo member, Func<HarmonyMethod, T> getter)
    {
        if (member is null || getter is null) return default;
        return member.GetCustomAttributes<HarmonyAttribute>(inherit: true)
            .Where(a => a.info != null)
            .Select(a => getter.Invoke(a.info))
            .FirstOrDefault(res => res != null);
    }

    /// <summary>
    /// Determines if the given instruction references the address of a field.
    /// </summary>
    public static bool IsFieldAddressed(this CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldflda || instruction.opcode == OpCodes.Ldsflda;
    }
}