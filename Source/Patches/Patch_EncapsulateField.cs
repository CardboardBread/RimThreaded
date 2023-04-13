using Newtonsoft.Json;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Caching;
using System.Text;
using static HarmonyLib.Code;

namespace RimThreaded.Patches
{
    // Executing field-to-method conversion in a harmony patch class.
    // TODO: track all member replacements that add the constraint of no address usage, highlight possible conflicts in other mods' patches.
    [PatchCategory(RimThreadedHarmony.DestructiveCategory)]
    [HarmonyPatch]
    public static class Patch_EncapsulateField
    {
        private const string CacheFolderName = nameof(Patch_EncapsulateField);
        private static HashSet<EncapsulateFieldPatchAttribute> attributes = new();
        private static HashSet<FieldInfo> targetFields = new();
        private static Dictionary<FieldInfo, EncapsulateFieldPatchAttribute> attributesByField = new();

        private static MemoryCache TargetMethodsCache = new($"{nameof(Patch_EncapsulateField)}_{nameof(HarmonyTargetMethods)}");
        private static Dictionary<MethodBase, HashSet<int>> addressingTargetLocations = new();

        [HarmonyPrepare]
        public static bool Prepare(Harmony harmony, MethodBase original = null)
        {
            if (original == null)
            {
                // Grab all the encapsulate patches in this mod
                foreach (var attribute in RimThreadedMod.Instance.GetLocalAttributesByType<EncapsulateFieldPatchAttribute>())
                {
                    attributes.Add(attribute);
                    targetFields.Add(attribute.Target);
                    attributesByField[attribute.Target] = attribute;
                }

                return true;
            }
            else
            {
                return true;
            }
        }

        // Search for every method, constructor, getter and setter that references fields we are encapsulating.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmony)
        {
            foreach (var assembly in RimThreaded.GameAssemblies())
            {
                // Check if the methods we need have already been found before, and skip a bunch of expensive searching.
                if (!TryGetCachedResults(assembly, out var results))
                {
                    // No cached result found, so we have to manually search and cache those results
                    results = Search(assembly);
                    SetCachedResults(assembly, results);
                }

                foreach (var methodBase in results)
                {
                    yield return methodBase;
                }
            }

            // Search every method, constructor, getter and setter for targets.
            static IEnumerable<MethodBase> Search(Assembly assembly)
            {
                return from methodBase in assembly.AllMethodBases().AsParallel()
                       where ScanMethod(methodBase).Any()
                       select methodBase;
            }

            // Quickly scan the body of a method for any targeted fields
            static IEnumerable<int> ScanMethod(MethodBase method)
            {
                foreach (var ((opcode, operand), index) in RimThreadedHarmony.ReadMethodBody(method).WithIndex())
                {
                    if (IsInstructionTargeted(opcode, operand, out var isAddressed))
                    {
                        if (isAddressed)
                        {
                            addressingTargetLocations.NewValueIfAbsent(method).Add(index);
                        }
                        yield return index;
                    }
                }
            }

            static bool IsInstructionTargeted(OpCode opcode, object operand, out bool isAddressed)
            {
                isAddressed = false;
                if (operand is FieldInfo field && targetFields.Contains(field))
                {
                    if (opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
                    {
                        isAddressed = true;
                        Log.ErrorOnce($"The address of a field marked for rebinding is used, rebinding cannot be completed for: {field}", field.GetHashCode());
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }

            // Check if the memory cache has results still in-memory, otherwise check if results were saved to file.
            static bool TryGetCachedResults(Assembly assembly, out IEnumerable<MethodBase> results)
            {
                var memoryKey = GetMemoryKey(assembly);
                if (TargetMethodsCache.Get(memoryKey) is List<MethodBase> memoryResults)
                {
                    results = memoryResults;
                    return true;
                }

                var filePath = GetCacheFilePath(assembly);
                if (File.Exists(filePath))
                {
                    var fileText = File.ReadAllText(filePath);
                    var fileResults = JsonConvert.DeserializeObject<List<MethodBase>>(fileText);
                    results = fileResults;
                    return true;
                }

                results = Enumerable.Empty<MethodBase>();
                return false;
            }

            static void SetCachedResults(Assembly assembly, IEnumerable<MethodBase> results)
            {
                var resultsCopy = results.ToList();
                var memoryKey = GetMemoryKey(assembly);
                TargetMethodsCache.Set(memoryKey, resultsCopy, null);

                var filePath = GetCacheFilePath(assembly);
                var fileText = JsonConvert.SerializeObject(resultsCopy, Formatting.Indented);
                File.WriteAllText(filePath, fileText);
            }

            static string GetMemoryKey(Assembly assembly)
            {
                return assembly.ManifestModule.ModuleVersionId.ToString();
            }

            static string GetCacheFilePath(Assembly assembly)
            {
                var name = assembly.GetName().Name;
                var version = assembly.ManifestModule.ModuleVersionId;
                var filename = $"{name}-{version}.cache.json";
                return Path.Combine(RimThreadedMod.ExtrasFolderPath, CacheFolderName, filename);
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions,
                                                           ILGenerator iLGenerator,
                                                           MethodBase original)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.operand is FieldInfo field &&
                    attributesByField.TryGetValue(field, out var encapsulate) &&
                    encapsulate.IsPatchTarget(instruction))
                {
                    foreach (var insert in encapsulate.ApplyPatch(instruction))
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
            if (original == null)
            {
                if (!Prefs.DevMode)
                {
                    attributes.Clear();
                    attributes = null;
                    attributesByField.Clear();
                    attributesByField = null;
                    targetFields.Clear();
                    targetFields = null;
                    TargetMethodsCache.Dispose();
                }

                if (Prefs.LogVerbose && addressingTargetLocations.Any())
                {
                    Log.Message($"[RimThreaded] Issues discovered with executing {nameof(Patch_EncapsulateField)}: {addressingTargetLocations.Count} addressing conflicts discovered");
                    foreach (var method in addressingTargetLocations.Keys)
                    {
                        foreach (var index in addressingTargetLocations[method])
                        {
                            Log.Message($"{method.DeclaringType}.{method.Name}:{index}");
                        }
                    }
                }
            }
        }
    }
}
