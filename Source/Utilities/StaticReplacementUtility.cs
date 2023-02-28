using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.IO;

namespace RimThreaded.Utilities
{
    // basically rimthreaded's field replacement routine, but separated into its own area
    public static class StaticReplacementUtility
    {
        public static IEnumerable<ClassReplacement> LoadReplacementsJSON()
        {
            // get directory of mod from its content pack
            // look for the directory that contains the current running/loaded assembly
            // find a json file in there (next to the mod assembly) called replacements.json (version is determined by location context)
            // read the file contents into a string
            // use Newtonsoft.Json to deserialize and return the resulting list of replacements

            var jsonString = File.ReadAllText(RimThreadedMod.Instance.ReplacementsJsonPath);
            var replacements = JsonConvert.DeserializeObject<List<TypeReplacement>>(jsonString);
            var dynAssembly = new StaticReplacementAssembly("RimThreadedReplacements", "RimThreadedReplacements.dll");

            foreach (var type in replacements)
            {
                foreach (var field in type.FieldReplacements)
                {

                }

                foreach (var method in type.MethodReplacements)
                {

                }
            }
        }
    }

    public abstract class MemberReplacement
    {
        public MemberInfo Original;
        public MemberInfo Replacement;
        public TypeReplacement Parent;
        public abstract bool IsNew();
    }

    public class TypeReplacement : MemberReplacement
    {
        public new Type Original;
        public new Type Replacement;
        public TypeBuilder Builder => Replacement as TypeBuilder;
        public override bool IsNew() => Replacement is TypeBuilder;

        public List<FieldReplacement> FieldReplacements = new();
        public List<MethodReplacement> MethodReplacements = new();
    }

    public class FieldReplacement : MemberReplacement
    {
        public new FieldInfo Original;
        public new FieldInfo Replacement;
        public FieldBuilder Builder => Replacement as FieldBuilder;
        public override bool IsNew() => Replacement is FieldBuilder;
    }

    public class MethodReplacement : MemberReplacement
    {
        public new MethodInfo Original;
        public new MethodInfo Replacement;
        public MethodBuilder Builder => Replacement as MethodBuilder;
        public override bool IsNew() => Replacement is MethodBuilder;
    }

    // instances to hold reference to an assembly used for holding replacement types and their static members.
    // effectively holds, creates, and parses the runtime objects required to perform a member replacement.
    //      member replacement is basically creating a copy of a static member in a new assembly and modifying
    //      all references to that member in existing compiled assemblies. This maintains 'binary-compatibility'
    //      as any existing methods that reference the replaced member will have their instructions changed, but
    //      the original members remain unmodified. This operation could be considered in C/C++ as changing
    //      source files but not changing any header files.
    public class StaticReplacementAssembly
    {
        public AssemblyName Name;
        public AssemblyBuilder Assembly;
        public ModuleBuilder DynamicModule;

        public HashSet<Type> HandledTypes = new HashSet<Type>(); // set of classes we're working with

        public HashSet<MemberInfo> HandledMembers = new HashSet<MemberInfo>(); // set of members we're working with

        public Dictionary<Type, TypeBuilder> TypeReplacements = new Dictionary<Type, TypeBuilder>(); // linking types to their dynamic replacements
        public Dictionary<Type, Type> TypeRebindings = new Dictionary<Type, Type>(); // linking types to their existing replacements
        public Dictionary<TypeBuilder, Type> NewTypes = new Dictionary<TypeBuilder, Type>(); // linking dynamic types to their outputs
        public Dictionary<Type, List<MethodBuilder>> Methods;
        public Dictionary<MethodInfo, MethodBuilder> BuiltMethods;

        public Dictionary<FieldInfo, FieldInfo> FieldReplacements = new Dictionary<FieldInfo, FieldInfo>();
        public Dictionary<MethodInfo, MethodInfo> MethodReplacements = new Dictionary<MethodInfo, MethodInfo>();

        // Post-Load Fields
        public HashSet<Type> CreatedTypes = new HashSet<Type>(); // set of classes we're creating

        public StaticReplacementAssembly(string name, string fileName)
        {
            Name = new AssemblyName(name);
            Assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(Name, AssemblyBuilderAccess.RunAndSave);
            DynamicModule = Assembly.DefineDynamicModule(name, fileName);
        }

        public string ReplacingName(MemberInfo member) => $"{member.DeclaringType.Name}_Replacement";

        public void ParseFieldManifest(IEnumerable<FieldInfo> fields)
        {
            // make a replacement version for each field in respective replacement classes
            // add fields to tracker
        }

        public bool Check(MemberInfo member, Type replacementType = null)
        {
            if (member is FieldInfo field && !field.IsStatic ||
                member is MethodInfo method && !method.IsStatic ||
                member is ConstructorInfo constructor && !constructor.IsStatic)
            {
                Log.Error($"Cannot replace non-static member: {member}");
                return false;
                // TODO: replace non-static method with extension method?
            }

            if (HandledMembers.Contains(member))
            {
                Log.Warning($"Duplicate replacement of member: {member}");
                return false;
            }

            if (replacementType != null)
            {
                var matches = replacementType.FindMembers(
                    member.MemberType,
                    BindingFlags.DeclaredOnly,
                    (info, criteria) => info.Equals(member),
                    member);
            }

            return true;
        }

        public void Replace<A>(FieldInfo original, A attribute = null, Type replacementType = null, bool SelfInitialized = false) where A : Attribute
        {
            if (!Check(original, replacementType))
            {
                return;
            }

            FieldInfo newField;
            if (replacementType == null)
            {
                if (!TypeReplacements.TryGetValue(original.DeclaringType, out var typeBuilder))
                {
                    typeBuilder = DynamicModule.DefineType(ReplacingName(original), TypeAttributes.Public);
                    TypeReplacements[original.DeclaringType] = typeBuilder;
                }

                newField = typeBuilder.DefineField(original.Name, original.FieldType, original.Attributes | FieldAttributes.Public | FieldAttributes.Static);
            }
            else
            {
                newField = AccessTools.Field(replacementType, original.Name);
                if (!newField.HasAttribute<A>())
                {
                    Log.Error($"Type {replacementType} cannot replace Field {original}, replacing field does not possess the same attributes");
                    return;
                }

                TypeRebindings[original.DeclaringType] = replacementType;
            }

            HandledTypes.Add(original.DeclaringType);
            HandledMembers.Add(original);
            FieldReplacements[original] = newField;
        }

        public void Replace<A>(MethodInfo original, A attribute = null, Type replacementType = null, IEnumerable<CodeInstruction> instructions = null) where A : Attribute
        {
            // Verify correct parameters.
            if (!Check(original, replacementType))
            {
                return;
            }

            MethodInfo newMethod;
            if (replacementType == null)
            {
                // Make a new type if needed.
                if (!TypeReplacements.TryGetValue(original.DeclaringType, out var typeBuilder))
                {
                    typeBuilder = DynamicModule.DefineType(ReplacingName(original), TypeAttributes.Public);
                    TypeReplacements[original.DeclaringType] = typeBuilder;
                }

                // Make a new method, 
                var methodBuilder = typeBuilder.DefineMethod(original.Name, original.Attributes | MethodAttributes.Public | MethodAttributes.Static);
                newMethod = methodBuilder;
                BuiltMethods[newMethod] = methodBuilder;
                if (!Methods.TryGetValue(original.DeclaringType, out _))
                {
                    Methods[original.DeclaringType] = new List<MethodBuilder>();
                }
                Methods[original.DeclaringType].Add(methodBuilder);
            }
            else
            {
                newMethod = AccessTools.Method(original.DeclaringType, original.Name, original.GetParameters().Types(), original.GetGenericArguments());
                if (!newMethod.HasAttribute<A>())
                {
                    Log.Error($"Type {replacementType} cannot replace Method {original}, replacing method does not possess the same attributes");
                    return;
                }
            }

            HandledTypes.Add(original.DeclaringType);
            HandledMembers.Add(original);
            MethodReplacements[original] = newMethod;
        }
    }
}
