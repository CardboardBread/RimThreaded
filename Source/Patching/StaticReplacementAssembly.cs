using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Verse;
using System.Linq;
using RimThreaded.Utilities;

namespace RimThreaded.Patching;

// instances to hold reference to an assembly used for holding replacement types and their static members.
// effectively holds, creates, and parses the runtime objects required to perform a member replacement.
//      member replacement is basically creating a copy of a static member in a new assembly and modifying
//      all references to that member in existing compiled assemblies. This maintains 'binary-compatibility'
//      as any existing methods that reference the replaced member will have their instructions changed, but
//      the original members remain unmodified. This operation could be considered in C/C++ as changing
//      source files but not changing any header files.
[Obsolete]
public class StaticReplacementAssembly
{
    public class CodeInstructionComparer : IEqualityComparer<CodeInstruction>
    {
        // Modification of equals so that either x or y parameter can be null to ignore
        public bool Equals(CodeInstruction x, CodeInstruction y)
        {
            // Same pattern from `InstructionReplacement`, `results.All(res => !res.HasValue || (res.HasValue && res.Value))`
            return (x.opcode == default && y.opcode == default) || ((x.opcode != default && y.opcode != default) && x.opcode == y.opcode)
                && (x.operand == null && y.operand == null) || ((x.operand != null && y.operand != null) && x.operand == y.operand)
                && (x.labels == null && y.labels == null) || ((x.labels != null && y.labels != null) && x.labels.SequenceEqual(y.labels))
                && (x.blocks == null && y.blocks == null) || ((x.blocks != null && y.blocks != null) && x.blocks.SequenceEqual(y.blocks));
        }

        public int GetHashCode(CodeInstruction obj)
        {
            return HashCode.Combine(obj.opcode.GetHashCode(), obj.operand?.GetHashCode(), obj.labels?.GetHashCode(), obj.blocks?.GetHashCode());
        }
    }

    public delegate IEnumerable<CodeInstruction> DirectCallback(CodeInstruction instruction, ILGenerator iLGenerator, MethodBase original);

    public static DirectCallback NoOp = (i, il, o) => Gen.YieldSingle(i);

    public static string ReplacingName(Type type) => $"{type.Name}_Replacement";
    public static string InitializerName(MemberInfo member) => $"Initialize_{member.Name}";

    public readonly AssemblyName Name;
    public readonly AssemblyBuilder Assembly;
    public readonly ModuleBuilder DynamicModule;

    // Dictionaries for easy mapping original members to replacements.
    private readonly Dictionary<Type, Type> replacingTypes = new();

    private readonly Dictionary<FieldInfo, FieldInfo> replacingFields = new();
    private readonly Dictionary<FieldInfo, MethodInfo> getteringFields = new();
    private readonly Dictionary<FieldInfo, MethodInfo> setteringFields = new();

    private readonly Dictionary<MethodInfo, MethodInfo> replacingMethods = new();
    private readonly Dictionary<MethodInfo, FieldInfo> fieldingSetters = new();
    private readonly Dictionary<MethodInfo, FieldInfo> fieldingGetters = new();

    private readonly HashSet<FieldInfo> requiresInitializer = new();
    private readonly Dictionary<FieldInfo, MethodBase> fieldInitializers = new();

    // Quick dictionary for mutating instructions, found by their opcode and operand.
    private readonly Dictionary<CodeInstruction, DirectCallback> directReplacements = new(new CodeInstructionComparer());

    public StaticReplacementAssembly(string name, string fileName = null)
    {
        Name = new AssemblyName(name);
        Assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(Name, AssemblyBuilderAccess.RunAndSave);
        DynamicModule = Assembly.DefineDynamicModule(name, fileName ?? $"{name}.dll");
    }

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

        replacingFields[original] = replacement;

        if (initializer != null)
        {
            requiresInitializer.Add(replacement);
            fieldInitializers[replacement] = initializer;
        }

        return replacement;
    }

    public void Replace(FieldInfo original, DirectCallback callback)
    {
        directReplacements[new(OpCodes.Stsfld, original)] = callback;
        directReplacements[new(OpCodes.Stfld, original)] = callback;
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

    public bool IsTypeReplaced(Type type) => dynamicReplacingTypes.ContainsKey(type);

    public TypeBuilder GetReplacingType(MemberInfo member) => GetReplacingType(member.DeclaringType);

    public TypeBuilder GetReplacingType(Type original)
    {
        if (!dynamicReplacingTypes.TryGetValue(original, out var builder))
        {
            builder = DynamicModule.DefineType(ReplacingName(original), TypeAttributes.Public);
            dynamicReplacingTypes[original] = builder;
        }
        return builder;
    }

    public bool HasDirectReplacement(CodeInstruction instruction) => directReplacements.ContainsKey((instruction.opcode, instruction.operand));

    internal bool TryDirectReplacement(CodeInstruction instruction, ILGenerator iLGenerator, MethodBase original)
    {
        if (directReplacements.TryGetValue((instruction.opcode, instruction.operand), out var action))
        {
            action.Invoke(instruction);
            return true;
        }
        return false;
    }

    // TODO: a way to replace static calls with instance calls, which requires prepending this instruction with some loading instructions.
    internal bool TryReplaceStaticCall(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo staticCall)
        {
            if (replacingMethods.TryGetValue(staticCall, out var replace))
            {
                instruction.operand = replace;
                return true;
            }
            else if (fieldingGetters.TryGetValue(staticCall, out var getterField))
            {
                instruction.opcode = OpCodes.Ldfld;
                instruction.operand = getterField;
                return true;
            }
            else if (fieldingSetters.TryGetValue(staticCall, out var setterField))
            {
                instruction.opcode = OpCodes.Stfld;
                instruction.operand = setterField;
                return true;
            }
        }

        return false;
    }

    internal bool TryReplaceInstanceCall(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo instanceCall)
        {
            throw new NotImplementedException();
        }

        return false;
    }

    internal bool TryReplaceInstanceFieldLoad(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo instanceLoad)
        {
            throw new NotImplementedException();
        }

        return false;
    }

    internal bool TryReplaceInstanceFieldAddress(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldflda && instruction.operand is FieldInfo instanceAddress)
        {
            throw new NotImplementedException();
        }

        return false;
    }

    internal bool TryReplaceStaticFieldLoad(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo staticLoad)
        {
            if (replacingFields.TryGetValue(staticLoad, out var field))
            {
                instruction.operand = field;
                return true;
            }

            else if (getteringFields.TryGetValue(staticLoad, out var getter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = getter;
                return true;
            }
        }

        return false;
    }

    internal bool TryReplaceStaticFieldAddress(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldsflda && instruction.operand is FieldInfo staticAddress)
        {
            throw new NotImplementedException();
        }

        return false;
    }

    internal bool TryReplaceInstanceFieldStore(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo instanceStore)
        {
            throw new NotImplementedException();
        }

        return false;
    }

    internal bool TryReplaceStaticFieldStore(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stsfld && instruction.operand is FieldInfo staticStore)
        {
            if (replacingFields.TryGetValue(staticStore, out var field))
            {
                instruction.operand = field;
                return true;
            }
            else if (setteringFields.TryGetValue(staticStore, out var setter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = setter;
                return true;
            }
        }

        return false;
    }
}