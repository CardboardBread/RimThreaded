using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Verse;
using System.Linq;
using RimThreaded.Utilities;

namespace RimThreaded.StaticReplacement
{
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

        // Dictionaries for easy mapping original members to replacements.
        private Dictionary<Type, TypeBuilder> DynamicReplacingTypes = new();
        private Dictionary<Type, Type> PremadeReplacingTypes = new();

        private Dictionary<FieldInfo, FieldInfo> ReplacingFields = new();
        private Dictionary<FieldInfo, MethodInfo> GetteringFields = new();
        private Dictionary<FieldInfo, MethodInfo> SetteringFields = new();

        private Dictionary<MethodInfo, MethodInfo> ReplacingMethods = new();
        private Dictionary<MethodInfo, FieldInfo> ReplacingSetters = new();
        private Dictionary<MethodInfo, FieldInfo> ReplacingGetters = new();

        internal Dictionary<(OpCode, object), Action<OpCode, object>> DirectReplacements = new();

        public StaticReplacementAssembly(string name, string fileName = null)
        {
            Name = new AssemblyName(name);
            Assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(Name, AssemblyBuilderAccess.RunAndSave);
            DynamicModule = Assembly.DefineDynamicModule(name, fileName ?? $"{name}.dll");
        }

        public static string ReplacingName(MemberInfo member) => ReplacingName(member.DeclaringType);
        public static string ReplacingName(Type original) => $"{original.Name}_Replacement";
        public static string InitializerName(MemberInfo member) => $"Initialize_{member.Name}";

        // replacement determines if you want to replace a field with an existing one (i.e. already written) or create a dynamic one
        // selfInitialized is whether an extra code snippet requires initialization
        // initializer is MethodBase to allow ConstructorInfo and MethodInfo, in case a regular method is needed for initialization
        public FieldInfo Replace(FieldInfo original, FieldInfo replacement = null, MethodBase initializer = null)
        {
            // Null coalescing for creating a new field in a replacing type.
            replacement ??= GetReplacingType(original).DefineField(
                original.Name,
                original.FieldType,
                original.Attributes | FieldAttributes.Public | FieldAttributes.Static);

            ReplacingFields[original] = replacement;
            RebindFields.Enqueue((original, replacement));

            if (initializer != null)
            {
                InitializeFields.Enqueue((replacement, initializer));
            }

            return replacement;
        }

        public void DiscoverInitializer(FieldInfo field, MethodBase initializer)
        {
            var initializerName = InitializerName(field);

            // if the field is dynamic or pre-existing
            if (DynamicModule.GetType(field.DeclaringType.FullName) is Type dynamicType)
            {
                DynamicInitializerRequests.Enqueue((field, initializer));
                if (DynamicModule.GetMethod(initializerName) is MethodBuilder dynamicMethod)
                {
                    // add new() and store in field
                    //dynamicMethod.GetILGenerator().Emit(OpCodes.Newobj, constructor);
                    //dynamicMethod.GetILGenerator().Emit(OpCodes.Stsfld, field);

                    RimThreadedHarmony.Instance.CategoryPatch<DestructivePatchAttribute>(dynamicMethod, postfix: initializer);
                }
                else
                {

                }
            }
            else if (field.DeclaringType.Module.GetMethod(initializerName) is MethodInfo existingMethod)
            {
                PremadeInitializerRequests.Enqueue((existingMethod, field, initializer));

            }
        }

        public void PopulateDynamicInitializers(IEnumerable<(FieldInfo, MethodBase)> requests)
        {
            requests.Do(r => populate(r.Item1, r.Item2));

            void populate(FieldInfo field, MethodBase initializer)
            {
                var dynamicInit = GetDynamicInitializer(field);
                var ilg = dynamicInit.GetILGenerator();

                if (initializer is ConstructorInfo constructor)
                {
                    ilg.Emit(OpCodes.Newobj, constructor);
                }
                else if (initializer is MethodInfo method)
                {
                    ilg.Emit(OpCodes.Call, method);
                }
                ilg.Emit(OpCodes.Stsfld, field);
            }
        }

        public void PopulatePremadeInitializer(MethodInfo existingMethod, FieldInfo field, MethodBase initializer)
        {
            if (initializer is ConstructorInfo constructor)
            {
                // transpile constructor invocation on the end of the premade?
                // generate stub methods to postfix?
                throw new Exception("TBD");
            }
            else if (initializer is MethodInfo method)
            {
                RimThreadedHarmony.Instance.CategoryPatch<DestructivePatchAttribute>(existingMethod, postfix: new(method));
            }
        }

        public void Rebind(FieldInfo field)
        {

        }

        public void AppendAttribute<A>(FieldBuilder field) where A : Attribute, new()
        {
            var constructor = typeof(A).GetConstructor(Array.Empty<Type>());
            var attributeBuilder = new CustomAttributeBuilder(constructor, Array.Empty<object>());
        }

        public MethodBuilder Replace(MethodInfo method)
        {
            var replacingType = GetReplacingType(method);
            var replacingMethod = replacingType.DefineMethod(
                method.Name,
                method.Attributes | MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                method.ReturnType,
                method.GetParameters().Select(pi => pi.ParameterType).ToArray());
            DynamicReplacingMethods[method] = replacingMethod;
            return replacingMethod;
        }

        public MethodBuilder GetDynamicInitializer(FieldInfo field)
        {
            if (field.DeclaringType is not TypeBuilder typeBuilder)
            {
                throw new ArgumentException($"{field} must be member of dynamic type");
            }

            var initializerName = InitializerName(field);
            if (typeBuilder.GetMethod(initializerName) is not MethodBuilder methodBuilder)
            {
                methodBuilder = typeBuilder.DefineMethod(
                    initializerName,
                    MethodAttributes.Public | MethodAttributes.Static,
                    CallingConventions.Standard,
                    typeof(void),
                    Array.Empty<Type>());
            }

            return methodBuilder;
        }

        public bool IsTypeReplaced(Type type) => DynamicReplacingTypes.ContainsKey(type);

        public TypeBuilder GetReplacingType(MemberInfo member) => GetReplacingType(member.DeclaringType);

        public TypeBuilder GetReplacingType(Type original)
        {
            if (!DynamicReplacingTypes.TryGetValue(original, out var builder))
            {
                builder = DynamicModule.DefineType(ReplacingName(original), TypeAttributes.Public);
                DynamicReplacingTypes[original] = builder;
            }
            return builder;
        }

        // TODO: a way to replace static calls with instance calls, which requires prepending this instruction with some loading instructions.
        internal bool TryReplaceStaticCall(ref CodeInstruction instruction, MethodInfo staticCall)
        {
            if (ReplacingMethods.TryGetValue(staticCall, out var method))
            {
                instruction.operand = method;
            }

            else if (ReplacingGetters.TryGetValue(staticCall, out var gField))
            {
                instruction.opcode = OpCodes.Ldfld;
                instruction.operand = gField;
            }

            else if (ReplacingSetters.TryGetValue(staticCall, out var sField))
            {
                instruction.opcode = OpCodes.Stfld;
                instruction.operand = sField;
            }

            else
            {
                return false;
            }

            return true;
        }

        internal bool TryDirectReplacement(CodeInstruction instruction)
        {
            if (DirectReplacements.TryGetValue((instruction.opcode, instruction.operand), out var action))
            {
                action.Invoke(instruction.opcode, instruction.operand);
                return true;
            }
            return false;
        }

        internal bool TryReplaceInstanceCall(ref CodeInstruction instruction, MethodInfo instanceCall)
        {
            throw new NotImplementedException();
        }

        internal bool TryReplaceInstanceFieldLoad(ref CodeInstruction instruction, FieldInfo instanceLoad)
        {
            throw new NotImplementedException();
        }

        internal bool TryReplaceInstanceFieldAddress(ref CodeInstruction instruction, FieldInfo instanceAddress)
        {
            throw new NotImplementedException();
        }

        internal bool TryReplaceStaticFieldLoad(ref CodeInstruction instruction, FieldInfo staticLoad)
        {
            if (ReplacingFields.TryGetValue(staticLoad, out var field))
            {
                instruction.operand = field;
            }

            else if (GetteringFields.TryGetValue(staticLoad, out var getter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = getter;
            }

            else
            {
                return false;
            }

            return true;
        }

        internal bool TryReplaceStaticFieldAddress(ref CodeInstruction instruction, FieldInfo staticAddress)
        {
            throw new NotImplementedException();
        }

        internal bool TryReplaceInstanceFieldStore(ref CodeInstruction instruction, FieldInfo instanceStore)
        {
            throw new NotImplementedException();
        }

        internal bool TryReplaceStaticFieldStore(ref CodeInstruction instruction, FieldInfo staticStore)
        {
            if (ReplacingFields.TryGetValue(staticStore, out var field))
            {
                instruction.operand = field;
            }

            else if (SetteringFields.TryGetValue(staticStore, out var setter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = setter;
            }

            else
            {
                return false;
            }

            return true;
        }
    }
}
