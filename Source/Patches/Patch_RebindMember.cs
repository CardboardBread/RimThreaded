using HarmonyLib;
using RimThreaded.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Caching;
using Verse;
using static HarmonyLib.Code;

namespace RimThreaded.Patches
{
    // Executing static member rebinding in a harmony patch class, to hand over patching control to the local harmony instance.
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch]
    public static class Patch_RebindMember
    {
        public record struct CachedAssembly(string ModuleVersionId, List<MethodBase> Methods);

        internal const string CacheFolderName = $"Cache_{nameof(Patch_RebindMember)}";
        internal static string CacheFolderPath = Path.Combine(RimThreadedMod.ExtrasFolderPath, CacheFolderName);

        private static HashSet<RebindFieldPatchAttribute> rebindFields = new();
        private static HashSet<FieldInfo> targetFields = new();
        private static Dictionary<FieldInfo, RebindFieldPatchAttribute> rebindFieldsByField = new();
        private static Dictionary<FieldInfo, FieldBuilder> newFields = new();
        private static HashSet<FieldInfo> boxedFields = new();

        private static HashSet<RebindMethodPatchAttribute> rebindMethods = new();
        private static Dictionary<MethodInfo, RebindMethodPatchAttribute> rebindMethodsByMethod = new();

        private static ObjectCache nativeCache = new MemoryCache(CacheFolderName);
        private static Dictionary<string, System.WeakReference<List<MethodBase>>> methodsByAssembly = new();



        // Target every single method, constructor, getter and setter.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            foreach (var assembly in RimThreaded.GameAssemblies())
            {

            }

            foreach (var type in from assembly in GetOrFallback(typeof(Patch_RebindMember).FullName, () => AccessTools.AllAssemblies())
                                 from type in GetOrFallback(assembly.ManifestModule.ModuleVersionId.ToString(), () => AccessTools.GetTypesFromAssembly(assembly))
                                 select type)
            {
                foreach (var method in from method in AccessTools.GetDeclaredMethods(type)
                                       where ScanMethod(method)
                                       select method)
                {
                    yield return method;
                }

                foreach (var constructor in AccessTools.GetDeclaredConstructors(type))
                {
                    yield return constructor;
                }

                foreach (var property in AccessTools.GetDeclaredProperties(type))
                {
                    if (property.GetMethod != null)
                    {
                        yield return property.GetMethod;
                    }

                    if (property.SetMethod != null)
                    {
                        yield return property.SetMethod;
                    }
                }

            }

            static bool ScanMethod(MethodBase method)
            {
                var body = PatchProcessor.ReadMethodBody(method);
                return body.Any(IsInstructionTargeted);
            }

            static bool IsInstructionTargeted(KeyValuePair<OpCode, object> pair)
            {
                var (opcode, operand) = pair;
                if (operand is FieldInfo field && targetFields.Contains(field))
                {
                    if (opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
                    {
                        Log.ErrorOnce($"The address of a field marked for rebinding is used, rebinding cannot be completed for: {field}", field.GetHashCode());
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static T GetOrFallback<T>(string key, Func<T> fallback) where T : class
        {
            if (nativeCache.Get(key) is not T value)
            {
                value = fallback();
                nativeCache.Set(key, value, DateTimeOffset.Now.AddMinutes(10));
            }
            return value;
        }

        [HarmonyPrepare]
        public static bool Prepare(Harmony harmony, MethodBase original)
        {
            if (original == null)
            {
                foreach (var rebind in RimThreadedMod.Instance.GetLocalAttributesByType<RebindFieldPatchAttribute>())
                {
                    rebindFields.Add(rebind);
                    targetFields.Add(rebind.Target);
                    rebindFieldsByField[rebind.Target] = rebind;
                }

                foreach (var replace in RimThreadedMod.Instance.GetLocalAttributesByType<RebindMethodPatchAttribute>())
                {
                    rebindMethods.Add(replace);
                    rebindMethodsByMethod[replace.Target] = replace;
                }

                return true;
            }
            else
            {
                SysDebug.WriteLineIf(Harmony.DEBUG, $"Rebinding members in {original}");
                return true;
            }
        }

        // TODO: if the replacement field is the same type of the original, just replace operands accessing the field. if the
        //       replacement is a ThreadLocal<T> where T is the original field's type, replace the store/load with proper calls
        //       and raise an error if the field's address is ever required.
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions,
                                                           ILGenerator iLGenerator,
                                                           MethodBase original)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.operand is FieldInfo field &&
                    rebindFieldsByField.TryGetValue(field, out var fRebind) &&
                    fRebind.IsPatchTarget(instruction))
                {
                    SysDebug.WriteLine("TBD"); // writeline replacing instruction with rebind
                    foreach (var insert in fRebind.ApplyPatch(instruction))
                    {
                        yield return insert;
                    }
                }
                else if (instruction.operand is MethodInfo method &&
                    rebindMethodsByMethod.TryGetValue(method, out var mRebind) &&
                    mRebind.IsPatchTarget(instruction))
                {
                    foreach (var insert in mRebind.ApplyPatch(instruction))
                    {
                        yield return insert;
                    }
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        [HarmonyCleanup]
        public static void Cleanup(Harmony harmony, MethodBase original = null, Exception ex = null)
        {
            if (original == null && !Prefs.DevMode)
            {
                rebindFields.Clear();
                rebindFieldsByField.Clear();
                newFields.Clear();
                rebindMethods.Clear();
                rebindMethodsByMethod.Clear();
            }
        }
    }
}
