global using SysDebug = System.Diagnostics.Debug;
global using UnityDebug = UnityEngine.Debug;
using System;
using System.Collections.Generic;
using RimThreaded.Utilities;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimThreaded;

// Class for handling all the mod-relevant actions/information of RimThreaded.
// TODO: for adding fields to existing types, ConditionalWeakTable
public class RimThreadedMod : Mod
{
    public const string PatchExportsFolderName = "PatchExports";
    public static readonly string ExtrasFolderPathName = typeof(RimThreadedMod).Namespace;

    public static string ExtrasFolderPath => GenFilePaths.FolderUnderSaveData(ExtrasFolderPathName);
    public static string VersionedExtrasFolderPath => Path.Combine(ExtrasFolderPath, ModVersion.ToString());
    public static string PatchExportsFolderPath => Path.Combine(ExtrasFolderPath, PatchExportsFolderName);
    public static string VersionedPatchExportsFolderPath => Path.Combine(ExtrasFolderPath, ModVersion.ToString());

    // TODO: About.xml might have a property for defining mod versions
    public static readonly Version ModVersion = typeof(RimThreadedMod).Assembly.GetName().Version;

    public static RimThreadedMod Instance => LoadedModManager.GetMod<RimThreadedMod>();

    /// <summary>
    /// To prevent every custom patch from independently scanning the project for attribute uses, do a single early scan for attributes on project types and members.
    /// </summary>
    public static event EventHandler<DiscoverAttributeEventArgs> DiscoverLocalAttributeEvent;
    
    /// <summary>
    /// For patches that must scan every method in the game, do a single scan and notify subscribers along the way.
    /// To prevent every custom patch from independently scanning every single method's instructions.
    /// </summary>
    public static event EventHandler<InstructionScanningEventArgs> GameInstructionScanningEvent;

    /// <summary>
    /// Mod entry point, called before instance constructor.
    /// </summary>
    static RimThreadedMod()
    {
        // Initialize every class in the mod, allows earliest possible event subscriptions
        foreach (var localType in typeof(RimThreadedMod).Assembly.GetTypes())
        {
            RuntimeHelpers.RunClassConstructor(localType.TypeHandle);
        }
    }

    // TODO: verify we can successfully postfix patch a method we're currently in; the stack trace leading to this
    //       constructor contains `LoadedModManager.LoadAllActiveMods`.
    //       As a backup, we can prefix/postfix `LoadedModManager.ClearCachedPatches` since it's the last method in
    //       `LoadedModManager.LoadAllActiveMods` to be called, and is only called by `LoadedModManager.LoadAllActiveMods`
    /// <summary>
    /// Search for and report non-Harmony conflicts with other mods.
    /// </summary>
    internal static void DiscoverModConflicts()
    {
        RTLog.Message("Discovering mod conflicts...");
        // TODO: 
    }

    // TODO: ExportTranspiledMethods alongside config file(s). 
    // TODO: move this to RimThreadedHarmony since it has to do with patching
    public static void ExportTranspiledMethods() {}
    
    // TODO: verify attribute usage is a static member
    private static void LocateLocalAttribute(object sender, DiscoverAttributeEventArgs eventArgs)
    {
        var isAnyFailed = false;
        var failures = new List<Exception>();
        foreach (var attribute in eventArgs.Attributes)
        {
            if (attribute is ILocationAware locationAware)
            {
                try
                {
                    locationAware.Locate(eventArgs.Member);
                }
                catch (Exception ex)
                {
                    RTLog.Error($"Error in locating `{locationAware}` on `{eventArgs.Member}`: {ex}");
                    isAnyFailed = true;
                    failures.Add(ex);
                }
            }
        }

        if (isAnyFailed) throw new AggregateException(failures);
    }

    public readonly RimThreadedHarmony HarmonyInst;
    public readonly RimThreadedSettings Settings;
    public readonly Thread MainThread;
    public readonly SynchronizationContext MainSyncContext;
    
    public string GameVersion => Content.ModMetaData.SupportedVersionsReadOnly.First().ToString(); // TODO: verify this is correct.
    public string AssembliesFolder => Path.Combine(Content.RootDir, GameVersion, "Assemblies");
    private string ReplacementsJsonPath => Path.Combine(AssembliesFolder, $"replacements_{GameVersion}.json");
    private string ReplacementsJsonText => File.ReadAllText(ReplacementsJsonPath);
    
    private readonly Dictionary<Type, List<object>> _localAttributesByType = new();

    /// <summary>
    /// Mod entry point, called from <see cref="LoadedModManager.CreateModClasses"/>.
    /// </summary>
    public RimThreadedMod(ModContentPack content) : base(content)
    {
        RTLog.Message($"Initializing version {ModVersion}...");
        MainThread = Thread.CurrentThread;
        MainSyncContext = SynchronizationContext.Current;
        DiscoverLocalAttributeEvent += RegisterLocalAttribute;

        bool originalHarmonyDebug = Harmony.DEBUG;
        if (Prefs.LogVerbose)
        {
            RTLog.Message(
                $"{nameof(Prefs.LogVerbose)} preference enabled, enabling Harmony full debugging mode for RimThreaded patching...");
            Harmony.DEBUG = true;
        }

        Settings = GetSettings<RimThreadedSettings>();
        HarmonyInst = new(content.PackageId);

        // Single pass attribute discovery
        // Manually call LocateLocalAttribute() first, so attributes are located before others use them
        RTLog.Message("Discovering local attributes");
        foreach (var localType in typeof(RimThreadedMod).Assembly.GetTypes())
        {
            var typeAttributes = localType.GetCustomAttributes(inherit: true);
            var typeEvent = new DiscoverAttributeEventArgs(localType, typeAttributes);
            
            LocateLocalAttribute(this, typeEvent);
            DiscoverLocalAttributeEvent?.Invoke(this, typeEvent);

            foreach (var localMember in localType.GetMembers(AccessTools.allDeclared))
            {
                var memberAttributes = localMember.GetCustomAttributes(inherit: true);
                var memberEvent = new DiscoverAttributeEventArgs(localMember, memberAttributes);
                
                LocateLocalAttribute(this, memberEvent);
                DiscoverLocalAttributeEvent?.Invoke(this, memberEvent);
            }
        }
        
        // Single pass instruction scanning
        foreach (var assembly in RimThreaded.GameAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var methodBase in type.AllMethodBases())
                {
                    var methodBody = RimThreadedHarmony.ReadMethodBody(methodBase);
                    var methodEvent = new InstructionScanningEventArgs(assembly, type, methodBase, methodBody);
                    GameInstructionScanningEvent?.Invoke(this, methodEvent);
                }
            }
        }

        RTLog.Message("Patching methods...");
        foreach (var localType in typeof(RimThreadedMod).Assembly.GetTypes())
        {
            
        }
        // Apply field replacement patch(es)
        // Apply destructive patches
        // Apply default patches
        // Apply non-destructive patches
        // Apply mod compatibility patches
        RTLog.Message("Patching is complete.");

        // Conditionally export transpiled methods
        if (Settings.ExportTranspiledMethods)
        {
            ExportTranspiledMethods();
        }

        if (Prefs.LogVerbose)
        {
            RTLog.Message(
                $"{nameof(Prefs.LogVerbose)} preference enabled, returning Harmony full debugging mode to original state...");
            Harmony.DEBUG = originalHarmonyDebug;
        }

        RTLog.Message($"Finished initializing version {ModVersion}.");
    }

    // A convenience method so any code that needs every single local declaration of a local attribute, doesn't have to scan the whole assembly for them.
    public IEnumerable<T> GetLocalAttributesByType<T>() where T : Attribute =>
        _localAttributesByType.GetValueSafe(typeof(T))?.Cast<T>();

    private void RegisterLocalAttribute(object sender, DiscoverAttributeEventArgs eventArgs)
    {
        foreach (var attribute in eventArgs.Attributes)
        {
            var type = attribute.GetType();
            if (type.Assembly == typeof(RimThreadedMod).Assembly)
            {
                _localAttributesByType.NewValueIfAbsent(type).Add(attribute);
            }
        }
    }

    public override void WriteSettings() => base.WriteSettings();

    public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);

    public override string SettingsCategory() => typeof(RimThreadedMod).Namespace;
}