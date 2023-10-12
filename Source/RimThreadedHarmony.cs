global using HarmonyMethodBody = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.Reflection.Emit.OpCode, object>>;
using RimThreaded.Patches;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace RimThreaded
{
    // Class for handling all of RimThreaded's complex interaction with Harmony.
    // Class instance is a wrapper for a Harmony instance, allowing patching only specific groups (non-destructive vs destructive).
    public class RimThreadedHarmony : Harmony
	{
        /// <summary>Tuple-like type for modelling potential conflicts between RimThreaded Harmony patches and foreign Harmony patches.</summary>
        /// <param name="Original">The method/constructor/getter/setter that a Harmony patch conflict was detected on.</param>
        /// <param name="Local">The RimThreaded Harmony patch that has the potential to conflict with foreign Harmony patches.</param>
        /// <param name="Foreigns">A collection of foreign Harmony patches that may conflict with a RimThreaded Harmony patch.</param>
        public record struct PatchConflicts(MethodBase Original, Patch Local, IEnumerable<Patch> Foreigns);

        public const string DynamicAssemblyName = "RimThreadedDynamic";

        // TODO: prepend with RimThreaded if categories could clash
        public const string DefaultCategory = "";
        public const string DestructiveCategory = "Destructive";
        public const string NonDestructiveCategory = "NonDestructive";
        public const string ModCompatibilityCategory = "ModCompatibility";

        public static Assembly DynamicAssembly => _DynamicAssembly;
        public static Module DynamicModule => _DynamicModule;
        public static RimThreadedHarmony Instance => RimThreadedMod.Instance.HarmonyInst;
        public static readonly Assembly RimWorldSource = typeof(Game).Assembly;

        internal static readonly AssemblyName _DynamicAssemblyName;
        internal static readonly AssemblyBuilder _DynamicAssembly;
        internal static readonly ModuleBuilder _DynamicModule;
        internal static readonly Dictionary<Type, TypeBuilder> _TypeReplacements = new();
        internal static string PatchConflictsText => patchConflictsText;

        private static List<PatchConflicts> conflictingPatches;
        private static string patchConflictsText;
        private static readonly ConditionalWeakTable<MethodBase, HarmonyMethodBody> methodBodyCache = new();

        static RimThreadedHarmony()
		{
			_DynamicAssemblyName = new(DynamicAssemblyName);
			_DynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(_DynamicAssemblyName, AssemblyBuilderAccess.RunAndSave);
            _DynamicModule = _DynamicAssembly.DefineDynamicModule(DynamicAssemblyName, $"{DynamicAssemblyName}.dll");
        }

        /// <summary>
        /// Caching wrapper for <see cref="PatchProcessor.ReadMethodBody()"/>.
        /// </summary>
        public static HarmonyMethodBody ReadMethodBody(MethodBase method)
        {
            return methodBodyCache.GetValue(method, PatchProcessor.ReadMethodBody);
        }

        internal static bool IsTypeReplaced(Type type)
        {
            return _TypeReplacements.ContainsKey(type);
        }

        // Common interface for reducing duplication of dynamic types designed to hold members that replace existing members.
        internal static TypeBuilder GetReplacingType(Type type)
        {
            if (!_TypeReplacements.TryGetValue(type, out var builder))
            {
                builder = _DynamicModule.DefineType(GetReplacingTypeName(type), type.Attributes);
                _TypeReplacements[type] = builder;
            }
            return builder;
        }

        private static string GetReplacingTypeName(Type type) => $"{type.Name}_Replacement";

        internal static TypeBuilder GetReplacingType(ModuleBuilder module, Type type)
        {
            if (!_TypeReplacements.TryGetValue(type, out var builder))
            {
                builder = module.DefineType(GetReplacingTypeName(type), type.Attributes);
                _TypeReplacements[type] = builder;
            }
            return builder;
        }

        internal static IEnumerable<PatchConflicts> GetAllConflictingPatches() => Harmony.GetAllPatchedMethods().SelectMany(GetConflictingPatches);

        // TODO: find all patched methods that have at least one patch from RimThreaded which is not in the non-destructive
        //       category and is not the only patch on a given method.
        // TODO: for each above patch, associate each nearby patch (other prefixes in this case) as a potential conflict if
        //       it has a higher priority.
        private static IEnumerable<PatchConflicts> GetConflictingPatches(MethodBase original)
        {
            if (Harmony.GetPatchInfo(original) is not HarmonyLib.Patches patches || !patches.Owners.Any(o => o != RimThreadedHarmony.Instance.Id))
            {
                yield break;
            }

            // Ensure a local prefix has a foreign prefix it could potentially conflict with.
            // TODO: this but for postfixes, transpilers and finalizers.
            if (patches.Prefixes.Count() < 2 &&
                patches.Postfixes.Count() < 1 &&
                patches.Transpilers.Count() < 1 &&
                patches.Prefixes.Any(IsLocalPatch) &&
                !patches.Prefixes.Any(IsForeignPatch))
            {
                yield break;
            }

            var conflictingPrefixes = patches.Prefixes.Where(MaybeConflictingPatch);
            var (highestPriority, lowestPriority) = GetPriorityLimits(conflictingPrefixes);
            foreach (var conflictingPrefix in conflictingPrefixes)
            {
                var higherPriorityForeignPrefixes = patches.Prefixes.Where(p => IsForeignPatch(p) && p.priority >= conflictingPrefix.priority);
                if (higherPriorityForeignPrefixes.Count() > 0)
                {
                    yield return new(original, conflictingPrefix, higherPriorityForeignPrefixes);
                }
            }

            static bool IsLocallyPatched(HarmonyLib.Patches patches)
                => patches.Prefixes.Any(IsLocalPatch)
                || patches.Postfixes.Any(IsLocalPatch)
                || patches.Transpilers.Any(IsLocalPatch)
                || patches.Finalizers.Any(IsLocalPatch);

            static bool IsLocalPatch(Patch patch) => patch.owner == RimThreadedHarmony.Instance.Id;

            static bool IsForeignPatch(Patch patch) => patch.owner != RimThreadedHarmony.Instance.Id;

            static bool MaybeConflictingPatch(Patch patch)
            {
                return IsLocalPatch(patch) && patch.PatchMethod.HarmonyPatchCategory() != RimThreadedHarmony.NonDestructiveCategory;
            }

            static (int highest, int lowest) GetPriorityLimits(IEnumerable<Patch> conflictingPrefixes)
            {
                var highestPriority = Priority.Last;
                var lowestPriority = Priority.First;

                foreach (var prefix in conflictingPrefixes)
                {
                    if (prefix.priority > highestPriority)
                    {
                        highestPriority = prefix.priority;
                    }

                    if (prefix.priority < lowestPriority)
                    {
                        lowestPriority = prefix.priority;
                    }
                }

                return (highestPriority, lowestPriority);
            }
        }

        internal static string GetPatchConflictsText(IEnumerable<PatchConflicts> conflicts)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Discovered {conflicts.Count()} potential Harmony patch conflicts:");
            foreach (var patch in conflicts)
            {
                builder.AppendLine($"\t---Harmony Patched Method: `{patch.Original.FullDescription()}`---");
                builder.AppendLine($"\tRimThreaded Patch Method: `{patch.Local.PatchMethod.FullDescription()}` priority: {patch.Local.priority}");
                foreach (var foreign in patch.Foreigns)
                {
                    builder.AppendLine($"\t\t`{foreign.patchMethod.FullDescription()}` owner: {foreign.owner}, priority: {foreign.priority}");
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Search for and report conflicts between Harmony patches in this mod and Harmony patches in other mods.
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadAllActiveMods))]
        public static void DiscoverPatchConflicts()
        {
            RTLog.Message("Discovering potential Harmony patch conflicts...");
            conflictingPatches ??= GetAllConflictingPatches().ToList();
            patchConflictsText ??= GetPatchConflictsText(conflictingPatches);
            if (Prefs.LogVerbose && conflictingPatches.Count > 0)
            {
                RTLog.Warning(patchConflictsText);
            }
        }

        internal static void ExportTranspiledMethods()
        {

        }

        private readonly HashSet<string> enabledCategories = new();
        private readonly Dictionary<string, List<ReplacePatchesSourceAttribute>> multiReplacePatchesByGroup = new();
        private readonly Dictionary<string, List<ReplacePatchSourceAttribute>> singleReplacePatchesByGroup = new();
        private readonly Dictionary<string, List<Action<Harmony>>> manualPatchesByGroup = new();
		
        public RimThreadedHarmony(string id) : base(id)
        {
            enabledCategories.Add(DefaultCategory);
            enabledCategories.Add(NonDestructiveCategory);
            enabledCategories.Add(DestructiveCategory);
        }

        public bool IsCategoryEnabled(string category) => enabledCategories.Contains(category);

        internal void DiscoverAttribute(MemberInfo member, object[] attributes = null)
        {
            attributes ??= member.GetCustomAttributes(inherit: true);

            foreach (var attribute in attributes)
            {
                try
                {
                    discoverReplacePatch(member, attribute);
                }
                catch (Exception ex)
                {
                    RTLog.Error($"Error in {attribute} locating: {ex}");
                }
            }

            void discoverReplacePatch(MemberInfo member, object attribute)
            {
                if (attribute is ReplacePatchSourceAttribute singleReplacePatch)
                {
                    var category = member.HarmonyPatchCategory();
                    singleReplacePatchesByGroup.NewValueIfAbsent(category).Add(singleReplacePatch);
                }

                if (attribute is ReplacePatchesSourceAttribute multiReplacePatch)
                {
                    var category = member.HarmonyPatchCategory();
                    multiReplacePatchesByGroup.NewValueIfAbsent(category).Add(multiReplacePatch);
                }
            }
        }

        internal void PatchLater(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null, string category = null)
        {
            category ??= DefaultCategory;
            void laterPatch(Harmony h) => h.Patch(original, prefix, postfix, transpiler, finalizer);
            manualPatchesByGroup.NewValueIfAbsent(category).Add(laterPatch);
        }
    }
}
