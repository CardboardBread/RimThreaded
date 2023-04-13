using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Patching
{
    // Using direct casts instead of the 'as' operator, because invalid casts throwing exceptions are a good indicator
    // for out-of-date code.
    // TODO: bind to current assembly version of Harmony
    [Obsolete]
    public static class PatchClassProcessorExtensions
    {
        private static FieldInfo _PatchClassProcessor_patchMethods = typeof(PatchClassProcessor).GetField("patchMethods");
        private static FieldInfo _PatchClassProcessor_auxilaryMethods = typeof(PatchClassProcessor).GetField("auxilaryMethods");
        private static FieldInfo _PatchClassProcessor_auxilaryTypes = typeof(PatchClassProcessor).GetField("auxilaryTypes");

        private static Type _AttributePatch_Type = AccessTools.TypeByName("HarmonyLib.AttributePatch");
        private static FieldInfo _AttributePatch_info = _AttributePatch_Type.GetField("info");

        private static List<object> _patchMethods(this PatchClassProcessor processor)
        {
            return (List<object>)_PatchClassProcessor_patchMethods.GetValue(processor);
        }

        private static Dictionary<Type, MethodInfo> _auxilaryMethods(this PatchClassProcessor processor)
        {
            return (Dictionary<Type, MethodInfo>)_PatchClassProcessor_auxilaryMethods.GetValue(processor);
        }

        private static List<Type> _auxilaryTypes()
        {
            return (List<Type>)_PatchClassProcessor_auxilaryTypes.GetValue(null);
        }

        private static HarmonyMethod _info(this object attributePatch)
        {
            return (HarmonyMethod)_AttributePatch_info.GetValue(attributePatch);
        }

        // Perform a Harmony.PatchAll, but only use methods tagged with a given Attribute.
        public static void AttributePatchAll<TargetAttribute>(this Harmony harmony) where TargetAttribute : Attribute
        {
            AccessTools.GetTypesFromAssembly(RimThreaded.LocalAssembly).Do(AttributeProcessorPatch);

            void AttributeProcessorPatch(Type type)
            {
                var processor = harmony.CreateClassProcessor(type);
                var auxilaryMethods = processor._auxilaryMethods();

                var allMethods = type.GetMethods(AccessTools.all).ToList();
                RebindHarmonyFunction<TargetAttribute, HarmonyPrepare>(allMethods, auxilaryMethods);
                RebindHarmonyFunction<TargetAttribute, HarmonyCleanup>(allMethods, auxilaryMethods);
                RebindHarmonyFunction<TargetAttribute, HarmonyTargetMethod>(allMethods, auxilaryMethods);
                RebindHarmonyFunction<TargetAttribute, HarmonyTargetMethods>(allMethods, auxilaryMethods);

                // Find all AttributePatch in PatchClassProcessor.patchMethods that are marked with the target attribute, remove the rest from the field.
                var patchMethods = processor._patchMethods();
                foreach (var patch in patchMethods)
                {
                    if (!isPatchTargeted<TargetAttribute>(patch))
                    {
                        patchMethods.Remove(patch);
                    }
                }

                processor.Patch();
            }
        }

        // Create a PatchClassProcessor that only uses methods that pass the given predicate.
        public static PatchClassProcessor FilteredProcessor(this Harmony harmony, Type type, Func<MethodInfo, bool> predicate)
        {
            var processor = harmony.CreateClassProcessor(type);
            var auxilaryMethods = processor._auxilaryMethods();

            var allMethods = type.GetMethods(AccessTools.all).ToList();
            RebindHarmonyFunction<HarmonyPrepare>(allMethods, auxilaryMethods, predicate);
            RebindHarmonyFunction<HarmonyCleanup>(allMethods, auxilaryMethods, predicate);
            RebindHarmonyFunction<HarmonyTargetMethod>(allMethods, auxilaryMethods, predicate);
            RebindHarmonyFunction<HarmonyTargetMethods>(allMethods, auxilaryMethods, predicate);

            // Find all AttributePatch in PatchClassProcessor.patchMethods that pass the predicate, remove the rest from the field.
            var patchMethods = processor._patchMethods();
            foreach (var patch in patchMethods)
            {
                if (!isPatchTargeted(patch, predicate))
                {
                    patchMethods.Remove(patch);
                }
            }

            return processor;
        }

        // Change a patch processor's function binding to one that has the target attribute, or remove any existing function binding.
        private static bool RebindHarmonyFunction<TargetAttribute, FunctionAttribute>(IEnumerable<MethodInfo> methods, Dictionary<Type, MethodInfo> auxilaryMethods) where TargetAttribute : Attribute where FunctionAttribute : Attribute
        {
            if (methods.FirstOrDefault(isHarmonyAttributeFunction<TargetAttribute, FunctionAttribute>) is MethodInfo info)
            {
                auxilaryMethods[typeof(FunctionAttribute)] = info;
                return true;
            }
            else
            {
                auxilaryMethods.Remove(typeof(FunctionAttribute));
                return false;
            }
        }

        private static bool RebindHarmonyFunction<FunctionAttribute>(IEnumerable<MethodInfo> methods,
                                                  Dictionary<Type, MethodInfo> auxilaryMethods,
                                                  Func<MethodInfo, bool> predicate) where FunctionAttribute : Attribute
        {
            if (methods.FirstOrDefault(m => m.HasAttribute<FunctionAttribute>() && predicate.Invoke(m)) is MethodInfo info)
            {
                auxilaryMethods[typeof(FunctionAttribute)] = info;
                return true;
            }
            else
            {
                auxilaryMethods.Remove(typeof(FunctionAttribute));
                return false;
            }
        }

        // Predicate to determine if a method is marked as a harmony function and with the target attribute.
        private static bool isHarmonyAttributeFunction<TargetAttribute, FunctionAttribute>(MethodInfo method) where TargetAttribute : Attribute where FunctionAttribute : Attribute
        {
            return method.HasAttribute<FunctionAttribute>() && method.HasAttribute<TargetAttribute>();
        }

        // Predicate for if a AttributePatch's target method is marked with the target attribute.
        private static bool isPatchTargeted<TargetAttribute>(object patch) where TargetAttribute : Attribute
        {
            return patch.GetType() == _AttributePatch_Type && patch._info().method.HasAttribute<TargetAttribute>();
        }

        private static bool isPatchTargeted(object patch, Func<MethodInfo, bool> predicate)
        {
            return patch.GetType() == _AttributePatch_Type && predicate.Invoke(patch._info().method);
        }
    }
}
