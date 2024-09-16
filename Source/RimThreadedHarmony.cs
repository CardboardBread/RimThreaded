global using HarmonyMethodBody = System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.Reflection.Emit.OpCode, object>>;
using System;
using System.Collections.Generic;
using RimThreaded.Patching;
using RimThreaded.Utilities;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Verse;

namespace RimThreaded;

/// <summary>
/// Class for handling all of RimThreaded's complex interaction with Harmony.
/// Class instance is a wrapper for a Harmony instance, allowing patching only specific groups (e.g; non-destructive vs destructive).
/// </summary>
public class RimThreadedHarmony : Harmony
{
    public const string DefaultDynamicAssemblyName = "RimThreadedDynamic";
    public const string DefaultDynamicAssemblyFileName = $"{DefaultDynamicAssemblyName}.dll";

    // TODO: prepend with RimThreaded if categories could clash
    public const string DefaultCategory = "";
    public const string DestructiveCategory = "Destructive";
    public const string NonDestructiveCategory = "NonDestructive";
    public const string ModCompatibilityCategory = "ModCompatibility";
        
    public static Assembly DynamicAssembly => DynamicAssemblyBuilder;
    public static Module DynamicModule => DynamicModuleBuilder;
    public static RimThreadedHarmony Instance => RimThreadedMod.Instance.HarmonyInst;
    public static readonly Assembly RimWorldSource = typeof(Game).Assembly;

    private static readonly AssemblyName DynamicAssemblyName = new(DefaultDynamicAssemblyName);
    private static readonly AssemblyBuilder DynamicAssemblyBuilder;
    private static readonly ModuleBuilder DynamicModuleBuilder;
    private static readonly Dictionary<Type, TypeBuilder> TypeReplacements = new();
    private static readonly ConditionalWeakTable<MethodBase, HarmonyMethodBody> MethodBodyCache = new();

    static RimThreadedHarmony()
    {
        DynamicAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(DynamicAssemblyName, AssemblyBuilderAccess.RunAndSave);
        DynamicModuleBuilder = DynamicAssemblyBuilder.DefineDynamicModule(DefaultDynamicAssemblyName, DefaultDynamicAssemblyFileName);
    }

    /// <summary>
    /// Caching wrapper for <see cref="PatchProcessor.ReadMethodBody(System.Reflection.MethodBase)"/>.
    /// </summary>
    public static HarmonyMethodBody ReadMethodBody(MethodBase method) => MethodBodyCache.GetValue(method, SaveMethodBody);

    // Use .ToArray() to prevent multiple enumeration, as the wrapped method returns straight from .Select()
    private static HarmonyMethodBody SaveMethodBody(MethodBase method) => PatchProcessor.ReadMethodBody(method).ToArray();

    public static bool IsTypeReplaced(Type type) => TypeReplacements.ContainsKey(type);

    /// <summary>
    /// Common interface for reducing duplication of dynamic types designed to hold members that replace existing members.
    /// </summary>
    public static TypeBuilder GetReplacingType(Type type) => GetReplacingType(DynamicModuleBuilder, type);

    private static string GetReplacingTypeName(Type type) => $"{type.Name}_Replacement";

    private static TypeBuilder GetReplacingType(ModuleBuilder module, Type type)
    {
        if (TypeReplacements.TryGetValue(type, out var builder)) return builder;

        var replacingName = GetReplacingTypeName(type);
        builder = module.DefineType(replacingName, type.Attributes);
        TypeReplacements[type] = builder;
        return builder;
    }
    
    public static void ExportTranspiledMethods()
    {

    }

    private readonly HashSet<string> _enabledCategories = new();
    private readonly Dictionary<string, List<ReplacePatchesSourceAttribute>> _multiReplacePatchesByGroup = new();
    private readonly Dictionary<string, List<ReplacePatchSourceAttribute>> _singleReplacePatchesByGroup = new();
    private readonly Dictionary<string, List<Action<Harmony>>> _manualPatchesByGroup = new();
		
    public RimThreadedHarmony(string id) : base(id)
    {
        _enabledCategories.Add(DefaultCategory);
        _enabledCategories.Add(NonDestructiveCategory);
        _enabledCategories.Add(DestructiveCategory);

        RimThreadedMod.DiscoverLocalAttributeEvent += DiscoverLocalAttribute;
    }

    public bool IsCategoryEnabled(string category) => _enabledCategories.Contains(category);

    private void DiscoverLocalAttribute(object sender, DiscoverAttributeEventArgs eventArgs)
    {
        var category = eventArgs.Member.HarmonyPatchCategory();
        
        foreach (var singleReplacePatch in eventArgs.Attributes.OfType<ReplacePatchSourceAttribute>())
        {
            _singleReplacePatchesByGroup.NewValueIfAbsent(category).Add(singleReplacePatch);
        }

        foreach (var multiReplacePatch in eventArgs.Attributes.OfType<ReplacePatchesSourceAttribute>())
        {
            _multiReplacePatchesByGroup.NewValueIfAbsent(category).Add(multiReplacePatch);
        }
    }

    /// <summary>
    /// Queue a manual harmony patch to be executed during the project-wide patching phase.
    /// </summary>
    public void PatchLater(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
        HarmonyMethod transpiler = null, HarmonyMethod finalizer = null, string category = DefaultCategory)
    {
        void LaterPatch(Harmony h) => h.Patch(original, prefix, postfix, transpiler, finalizer);
        _manualPatchesByGroup.NewValueIfAbsent(category).Add(LaterPatch);
    }
}