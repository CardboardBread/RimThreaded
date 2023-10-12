global using HarmonyLib;
global using RimWorld;
global using System;
global using System.Collections.Generic;
global using System.Reflection;
global using UnityEngine;
global using Verse;
global using SysDebug = System.Diagnostics.Debug;
global using UnityDebug = UnityEngine.Debug;
using RimThreaded.Utilities;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using System.Threading;

namespace RimThreaded
{
    // Class for handling all the mod-relevant actions/information of RimThreaded.
    // TODO: for adding fields to existing types, ConditionalWeakTable
    public class RimThreadedMod : Mod
    {
        public delegate void DiscoverAttributeDelegate(MemberInfo member, object[] attributes);

        public const string PatchExportsFolderName = "PatchExports";
        public static readonly string ExtrasFolderPathName = typeof(RimThreadedMod).Namespace;

        public static string ExtrasFolderPath => GenFilePaths.FolderUnderSaveData(ExtrasFolderPathName);
        public static string VersionedExtrasFolderPath => Path.Combine(ExtrasFolderPath, ModVersion.ToString());
        public static string PatchExportsFolderPath => Path.Combine(ExtrasFolderPath, PatchExportsFolderName);
        public static string VersionedPatchExportsFolderPath => Path.Combine(ExtrasFolderPath, ModVersion.ToString());

        public static readonly Version ModVersion = typeof(RimThreadedMod).Assembly.GetName().Version; // TODO: About.xml might have a property for defining mod versions

        public static RimThreadedMod Instance => LoadedModManager.GetMod<RimThreadedMod>();

        /// <summary>
        /// Mod entry point, called before instance constructor.
        /// </summary>
        static RimThreadedMod()
        {
        }

        // TODO: verify we can sucessfully postfix patch a method we're currently in; the stack trace leading to this
        //       constructor contains `LoadedModManager.LoadAllActiveMods`.
        //       As a backup, we can prefix/postfix `LoadedModManager.ClearCachedPatches` since it's the last method in
        //       `LoadedModManager.LoadAllActiveMods` to be called, and is only called by `LoadedModManager.LoadAllActiveMods`
        /// <summary>
        /// Search for and report non-Harmony conflicts with other mods.
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadAllActiveMods))]
        internal static void DiscoverModConflicts()
        {
            RTLog.Message("Discovering mod conflicts...");
            // TODO: 
        }

        // TODO: ExportTranspiledMethods alongside config file(s). 
        // TODO: move this to RimThreadedHarmony since it has to do with patching
        public static void ExportTranspiledMethods()
        {
            AssemblyName aName = new AssemblyName("RimWorldTranspiles");
            //PermissionSet requiredPermission = new PermissionSet(PermissionState.Unrestricted);
            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndSave);
            ConstructorInfo Constructor2 = typeof(SecurityPermissionAttribute).GetConstructors()[0];
            PropertyInfo skipVerificationProperty = Property(typeof(SecurityPermissionAttribute), "SkipVerification");
#if false
            CustomAttributeBuilder sv2 = new CustomAttributeBuilder(Constructor2, new object[] { SecurityAction.RequestMinimum }, 
                new PropertyInfo[] { skipVerificationProperty }, new object[] { true });            
            ab.SetCustomAttribute(sv2);
#endif

            //System.Security.AllowPartiallyTrustedCallersAttribute Att = new System.Security.AllowPartiallyTrustedCallersAttribute();            
            //ConstructorInfo Constructor1 = Att.GetType().GetConstructors()[0];
            //object[] ObjectArray1 = new object[0];
            //CustomAttributeBuilder AttribBuilder1 = new CustomAttributeBuilder(Constructor1, ObjectArray1);
            //ab.SetCustomAttribute(AttribBuilder1);
            ModuleBuilder modBuilder = ab.DefineDynamicModule(aName.Name, aName.Name + ".dll");
            UnverifiableCodeAttribute ModAtt = new System.Security.UnverifiableCodeAttribute();
            ConstructorInfo Constructor = ModAtt.GetType().GetConstructors()[0];
            object[] ObjectArray = new object[0];
            CustomAttributeBuilder ModAttribBuilder = new CustomAttributeBuilder(Constructor, ObjectArray);
            modBuilder.SetCustomAttribute(ModAttribBuilder);
            Dictionary<string, TypeBuilder> typeBuilders = new Dictionary<string, TypeBuilder>();
            IEnumerable<MethodBase> originalMethods = Harmony.GetAllPatchedMethods();
            foreach (MethodBase originalMethod in originalMethods)
            {
                Patches patches = Harmony.GetPatchInfo(originalMethod);
                int transpiledCount = patches.Transpilers.Count;
                if (transpiledCount == 0)
                    continue;
                if (originalMethod is MethodInfo methodInfo) // add support for constructors as well
                {
                    Type returnType = methodInfo.ReturnType;
                    string typeTranspiled = originalMethod.DeclaringType.FullName + "_Transpiled";
                    if (!typeBuilders.TryGetValue(typeTranspiled, out TypeBuilder tb))
                    {
                        tb = modBuilder.DefineType(typeTranspiled, TypeAttributes.Public);
                        typeBuilders[typeTranspiled] = tb;
                    }
                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                    List<Type> types = new List<Type>();

                    int parameterOffset = 1;
                    if (!methodInfo.Attributes.HasFlag(MethodAttributes.Static))
                    {
                        types.Add(methodInfo.DeclaringType);
                        parameterOffset = 2;
                    }
                    foreach (ParameterInfo parameterInfo in parameterInfos)
                    {
                        types.Add(parameterInfo.ParameterType);
                    }
                    MethodBuilder mb = tb.DefineMethod(originalMethod.Name, MethodAttributes.Public | MethodAttributes.Static, returnType, types.ToArray());
                    if (typeTranspiled.Equals("Verse.PawnGenerator_Transpiled") && !originalMethod.Name.Equals(""))
                        Log.Message(originalMethod.Name);
                    if (!methodInfo.Attributes.HasFlag(MethodAttributes.Static))
                    {
                        ParameterAttributes pa = new ParameterAttributes();
                        ParameterBuilder pb = mb.DefineParameter(1, pa, methodInfo.DeclaringType.Name);
                    }

                    foreach (ParameterInfo parameterInfo in parameterInfos)
                    {
                        ParameterAttributes pa = new ParameterAttributes();
                        if (parameterInfo.IsOut) pa |= ParameterAttributes.Out;
                        if (parameterInfo.IsIn) pa |= ParameterAttributes.In;
                        if (parameterInfo.IsLcid) pa |= ParameterAttributes.Lcid;
                        if (parameterInfo.IsOptional) pa |= ParameterAttributes.Optional;
                        if (parameterInfo.IsRetval) pa |= ParameterAttributes.Retval;
                        if (parameterInfo.HasDefaultValue) pa |= ParameterAttributes.HasDefault;
                        ParameterBuilder pb = mb.DefineParameter(parameterInfo.Position + parameterOffset, pa, parameterInfo.Name);
                        if (parameterInfo.HasDefaultValue && parameterInfo.DefaultValue != null)
                            pb.SetConstant(parameterInfo.DefaultValue);
                    }
                    ILGenerator il = mb.GetILGenerator();
                    //il.Emit(OpCodes.Nop);
                    //MethodCopier methodCopier = new MethodCopier(originalMethod, il);
                    //List<Label> endLabels = new List<Label>();
                    //_ = methodCopier.Finalize(null, endLabels, out var hasReturnCode);
                    //List<CodeInstruction> ciList = MethodCopier.GetInstructions(il, originalMethod, 9999);
#if false
                    MethodInfo methodInfo2 = UpdateWrapper(originalMethod, il);
#endif
                    //Log.Message(ciList.ToString());
                    //List<CodeInstruction> currentInstructions = PatchProcessor.GetCurrentInstructions(originalMethod);
                    //Dictionary<Label, Label> labels = new Dictionary<Label, Label>();

                    //MethodBody methodBody = methodInfo.GetMethodBody();
                    //IList<LocalVariableInfo> localvars = methodBody.LocalVariables;
                    //LocalBuilder[] localBuildersOrdered = new LocalBuilder[255];
                    //foreach (LocalVariableInfo localVar in localvars)
                    //{
                    //    Type type = localVar.LocalType;
                    //    LocalBuilder newLocalBuilder = il.DeclareLocal(type);
                    //    localBuildersOrdered[localVar.LocalIndex] = newLocalBuilder;
                    //}
                    //IList<ExceptionHandlingClause> exceptionHandlingClauses = methodBody.ExceptionHandlingClauses;

                    ////LocalBuilder[] localBuildersOrdered = new LocalBuilder[255];                        
                    ////int localBuildersOrderedMax = 0;
                    ////foreach (CodeInstruction currentInstruction in currentInstructions)
                    ////{
                    ////    object operand = currentInstruction.operand;
                    ////    if (operand is LocalBuilder localBuilder)
                    ////    {
                    ////        localBuildersOrdered[localBuilder.LocalIndex] = localBuilder;
                    ////        localBuildersOrderedMax = Math.Max(localBuildersOrderedMax, localBuilder.LocalIndex);
                    ////    }
                    ////}
                    ////Dictionary<LocalBuilder, LocalBuilder> localBuilders = new Dictionary<LocalBuilder, LocalBuilder>();
                    ////for (int i = 0; i <= localBuildersOrderedMax; i++)
                    ////{
                    ////    LocalBuilder localBuilderOrdered = localBuildersOrdered[i];
                    ////    if (localBuilderOrdered == null)
                    ////    {
                    ////        il.DeclareLocal(typeof(object));
                    ////    }
                    ////    else
                    ////    {
                    ////        LocalBuilder newLocalBuilder = il.DeclareLocal(localBuilderOrdered.LocalType);
                    ////        localBuilders.Add(localBuilderOrdered, newLocalBuilder);
                    ////    }
                    ////}

                    //foreach (CodeInstruction currentInstruction in currentInstructions)
                    //{
                    //    bool endFinally = false;
                    //    foreach (Label label in currentInstruction.labels)
                    //    {
                    //        if (!labels.TryGetValue(label, out Label translatedLabel))
                    //        {
                    //            translatedLabel = il.DefineLabel();
                    //            labels[label] = translatedLabel;
                    //        }
                    //        il.MarkLabel(translatedLabel);
                    //    }

                    //    //int i = il.ILOffset;
                    //    //foreach (ExceptionHandlingClause Clause in exceptionHandlingClauses)
                    //    //{
                    //    //    if (Clause.Flags != ExceptionHandlingClauseOptions.Clause &&
                    //    //       Clause.Flags != ExceptionHandlingClauseOptions.Finally)
                    //    //        continue;

                    //    //    // Look for an ending of an exception block first!
                    //    //    if (Clause.HandlerOffset + Clause.HandlerLength == i)
                    //    //        il.EndExceptionBlock();

                    //    //    // If this marks the beginning of a try block, emit that
                    //    //    if (Clause.TryOffset == i)
                    //    //        il.BeginExceptionBlock();

                    //    //    // Also check for the beginning of a catch block
                    //    //    if (Clause.HandlerOffset == i && Clause.Flags == ExceptionHandlingClauseOptions.Clause)
                    //    //        il.BeginCatchBlock(Clause.CatchType);

                    //    //    // Lastly, check for a finally block
                    //    //    if (Clause.HandlerOffset == i && Clause.Flags == ExceptionHandlingClauseOptions.Finally)
                    //    //        il.BeginFinallyBlock();
                    //    //}

                    //    foreach (ExceptionBlock block in currentInstruction.blocks)
                    //    {
                    //        switch (block.blockType) {
                    //            case ExceptionBlockType.BeginExceptionBlock:
                    //                {
                    //                    il.BeginExceptionBlock();
                    //                    break;
                    //                }
                    //            case ExceptionBlockType.BeginCatchBlock:
                    //                {
                    //                    il.BeginCatchBlock(block.catchType);
                    //                    break;
                    //                }
                    //            case ExceptionBlockType.BeginExceptFilterBlock:
                    //                {
                    //                    il.BeginExceptFilterBlock();
                    //                    break;
                    //                }
                    //            case ExceptionBlockType.BeginFaultBlock:
                    //                {
                    //                    il.BeginFaultBlock();
                    //                    break;
                    //                }
                    //            case ExceptionBlockType.BeginFinallyBlock:
                    //                {
                    //                    il.BeginFinallyBlock();
                    //                    break;
                    //                }
                    //            case ExceptionBlockType.EndExceptionBlock:
                    //                {
                    //                    //il.EndExceptionBlock();
                    //                    endFinally = true;
                    //                    break;
                    //                }
                    //            default:
                    //                {
                    //                    Log.Error("Unknown ExceptionBlock");
                    //                    break;
                    //                }
                    //        }
                    //    }

                    //    OpCode opcode = currentInstruction.opcode;
                    //    object operand = currentInstruction.operand;
                    //    switch (operand)
                    //    {
                    //        case null:
                    //            {
                    //                il.Emit(opcode);
                    //                break;
                    //            }
                    //        case byte operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case sbyte operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case short operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case int operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case MethodInfo operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case SignatureHelper operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case ConstructorInfo operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case Type operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case long operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case float operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case double operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case Label operandCasted:
                    //            {
                    //                if (!labels.TryGetValue(operandCasted, out Label translatedLabel))
                    //                {
                    //                    translatedLabel = il.DefineLabel();
                    //                    labels[operandCasted] = translatedLabel;
                    //                }
                    //                il.Emit(opcode, translatedLabel);
                    //                break;
                    //            }
                    //        case Label[] operandCasted:
                    //            {
                    //                List<Label> newLabels = new List<Label>();
                    //                foreach (Label operandCasted1 in operandCasted)
                    //                {
                    //                    if (!labels.TryGetValue(operandCasted1, out Label translatedLabel))
                    //                    {
                    //                        translatedLabel = il.DefineLabel();
                    //                        labels[operandCasted1] = translatedLabel;
                    //                    }
                    //                    newLabels.Add(translatedLabel);
                    //                }
                    //                il.Emit(opcode, newLabels.ToArray());
                    //                break;
                    //            }
                    //        case FieldInfo operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case string operandCasted:
                    //            {
                    //                il.Emit(opcode, operandCasted);
                    //                break;
                    //            }
                    //        case LocalBuilder operandCasted:
                    //            {
                    //                il.Emit(opcode, localBuildersOrdered[operandCasted.LocalIndex]);
                    //                break;
                    //            }
                    //        default:
                    //            {
                    //                Log.Error("UNKNOWN OPERAND");
                    //                break;
                    //            }
                    //    }
                    //    if (endFinally)
                    //    {
                    //        il.EndExceptionBlock();
                    //        //endFinally = true;
                    //    }
                    //}
                }
            }
            foreach (KeyValuePair<string, TypeBuilder> tb in typeBuilders)
            {

                tb.Value.CreateType();
            }
            ab.Save(aName.Name + ".dll");


            //ReImport DLL and create detour
            Assembly loadedAssembly = Assembly.UnsafeLoadFrom(aName.Name + ".dll");
            IEnumerable<TypeInfo> transpiledTypes = loadedAssembly.DefinedTypes;
            Dictionary<string, Type> tTypeDictionary = new Dictionary<string, Type>();
            foreach (TypeInfo transpiledType in transpiledTypes)
            {
                tTypeDictionary.Add(transpiledType.FullName, transpiledType.AsType());
            }

            foreach (MethodBase originalMethod in originalMethods)
            {
                Patches patches = Harmony.GetPatchInfo(originalMethod);
                int transpiledCount = patches.Transpilers.Count;
                if (transpiledCount > 0)
                {
                    if (originalMethod is MethodInfo methodInfo) // add support for constructors as well
                    {
                        Type transpiledType = tTypeDictionary[originalMethod.DeclaringType.FullName + "_Transpiled"];
                        ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                        List<Type> types = new List<Type>();

                        if (!methodInfo.Attributes.HasFlag(MethodAttributes.Static))
                        {
                            types.Add(methodInfo.DeclaringType);
                        }
                        foreach (ParameterInfo parameterInfo in parameterInfos)
                        {
                            types.Add(parameterInfo.ParameterType);
                        }
                        MethodInfo replacement = Method(transpiledType, originalMethod.Name, types.ToArray());
                        Memory.DetourMethod(originalMethod, replacement);
                    }
                }
            }
        }

        public readonly RimThreadedHarmony HarmonyInst;
        public readonly RimThreadedSettings Settings;
        public readonly string GameVersion;
        public readonly string AssembliesFolder;
        internal readonly Thread MainThread;
        internal readonly SynchronizationContext MainSyncContext;
        internal readonly string _ReplacementsJsonPath;
        internal readonly string _ReplacementsJsonText;
        private readonly Dictionary<Type, List<object>> localAttributesByType = new();

        /// <summary>
        /// Mod entry point, called from <see cref="LoadedModManager.CreateModClasses"/>.
        /// </summary>
        public RimThreadedMod(ModContentPack content) : base(content)
        {
            RTLog.Message($"Initializing version {ModVersion}...");
            MainThread = Thread.CurrentThread;
            MainSyncContext = SynchronizationContext.Current;

            bool originalHarmonyDebug = Harmony.DEBUG;
            if (Prefs.LogVerbose)
            {
                RTLog.Message($"{nameof(Prefs.LogVerbose)} preference enabled, enabling Harmony full debugging mode for RimThreaded patching...");
                Harmony.DEBUG = true;
            }

            Settings = GetSettings<RimThreadedSettings>();
            HarmonyInst = new(content.PackageId);
            GameVersion = content.ModMetaData.SupportedVersionsReadOnly.First().ToString(); // TODO: verify this is correct.
            AssembliesFolder = Path.Combine(content.RootDir, GameVersion, "Assemblies");
            _ReplacementsJsonPath = Path.Combine(AssembliesFolder, $"replacements_{GameVersion}.json");
            _ReplacementsJsonText = File.ReadAllText(_ReplacementsJsonPath);

            // Load replacements and patches
            GetAllLocalAttributes().Do(attr => DiscoverAttribute(attr, attr.GetCustomAttributes(inherit: true)));

            RTLog.Message($"Patching methods...");
            HarmonyInst.PatchAll();
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
                RTLog.Message($"{nameof(Prefs.LogVerbose)} preference enabled, returning Harmony full debugging mode to original state...");
                Harmony.DEBUG = originalHarmonyDebug;
            }

            RTLog.Message($"Finished initializing version {ModVersion}.");
        }

        // A convenience method so any code that needs every single local declaration of a local attribute, doesn't have to scan the whole assembly for them.
        public IEnumerable<T> GetLocalAttributesByType<T>() where T : Attribute => localAttributesByType.GetValueSafe(typeof(T))?.Cast<T>();

        // Rather than have each attribute search all local types for instances of itself, we'll do one pass over every local
        // type and try to initialize all the known attributes.
        internal IEnumerable<MemberInfo> GetAllLocalAttributes()
        {
            foreach (var type in RimThreaded.LocalTypes)
            {
                yield return type;

                foreach (var field in type.GetFields(AccessTools.allDeclared))
                {
                    yield return field;
                }

                foreach (var method in type.GetMethods(AccessTools.allDeclared))
                {
                    yield return method;
                }

                foreach (var constructor in type.GetConstructors(AccessTools.allDeclared))
                {
                    yield return constructor;
                }

                foreach (var property in type.GetProperties(AccessTools.allDeclared))
                {
                    yield return property;
                }
            }
        }

        // TODO: verify attribute usage is a static member
        internal void DiscoverAttribute(MemberInfo member, object[] attributes = null)
        {
            attributes ??= member.GetCustomAttributes(inherit: true);

            foreach (var attribute in attributes)
            {
                if (attribute is ILocationAware locationAware)
                {
                    try
                    {
                        locationAware.Locate(member);
                    }
                    catch (Exception ex)
                    {
                        RTLog.Error($"Error in locating `{locationAware}` on `{member}`: {ex}");
                    }
                }

                var attributeType = attribute.GetType();
                if (attributeType.Assembly == RimThreaded.LocalAssembly)
                {
                    localAttributesByType.NewValueIfAbsent(attributeType).Add(attribute);
                }
            }

            HarmonyInst.DiscoverAttribute(member, attributes);
        }

        public override void WriteSettings() => base.WriteSettings();

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);

        public override string SettingsCategory() => GetType().Namespace.Translate();
    }
}
