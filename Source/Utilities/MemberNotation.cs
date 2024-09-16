using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace RimThreaded.Utilities;

/// <summary>
/// Wrapper type for itemizing reflective lookups, such that they can be resolved later on in execution, or saved
/// to file and resolved in a separate process/application/time.
/// </summary>
[Serializable]
public record struct MemberNotation(string Declaring,
    string Name,
    string[] ParameterTypes,
    string[] GenericTypes,
    MemberTypes MemberType)
{
    public static implicit operator MemberNotation(MethodInfo method) => new(method);

    public static implicit operator MemberNotation(Type type) => new(type);

    public static implicit operator MemberNotation(FieldInfo field) => new(field);

    public static implicit operator MemberNotation(ConstructorInfo constructor) => new(constructor);

    public static implicit operator MethodInfo(MemberNotation notation) => notation.ResolveMethod();

    public static implicit operator Type(MemberNotation notation) => notation.ResolveType();

    public static implicit operator FieldInfo(MemberNotation notation) => notation.ResolveField();

    public static implicit operator ConstructorInfo(MemberNotation notation) => notation.ResolveConstructor();

    public static explicit operator MethodBase(MemberNotation notation) => ToAnyMethod(notation);

    public static explicit operator MemberNotation(MethodBase methodBase) => FromAnyMethod(methodBase);

    public static MethodBase ToAnyMethod(MemberNotation notation)
    {
        return notation.MemberType switch
        {
            MemberTypes.Method => notation.ResolveMethod(),
            MemberTypes.Constructor => notation.ResolveConstructor(),
            _ => throw new ArgumentException($"Notation does not describe a type subclassing {nameof(MethodBase)}")
        };
    }

    public static MemberNotation FromAnyMethod(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo method => method,
            ConstructorInfo constructor => constructor,
            _ => throw new NotImplementedException($"Unknown/Unsupported {nameof(MethodBase)} subclass")
        };
    }

    private static string[] AssemblyQualifiedGenericArguments(MethodBase methodBase)
    {
        return methodBase.GetGenericArguments().Select(type => type.AssemblyQualifiedName).ToArray();
    }

    private static string[] AssemblyQualifiedParameterTypes(MethodBase methodBase)
    {
        return methodBase.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName).ToArray();
    }

    /// <summary>
    /// Saved reference to the member used to initialize this struct, for reuse in the same process/application.
    /// </summary>
    [NonSerialized]
    public MemberInfo Member;

    public MemberNotation(MethodInfo method) : this(
        method.DeclaringType?.AssemblyQualifiedName,
        method.Name,
        AssemblyQualifiedParameterTypes(method),
        AssemblyQualifiedGenericArguments(method),
        method.MemberType)
    {
        Member = method;
        if (method is null) throw new ArgumentNullException(nameof(method));
    }

    public MemberNotation(Type type) : this(
        type.DeclaringType?.AssemblyQualifiedName,
        type.AssemblyQualifiedName,
        Array.Empty<string>(),
        Array.Empty<string>(),
        type.MemberType)
    {
        Member = type;
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (type.IsGenericParameter)
            throw new ArgumentException($"Building {nameof(MemberNotation)} for type parameters of generic methods is not supported", nameof(type));
    }

    public MemberNotation(FieldInfo field) : this(
        field.DeclaringType?.AssemblyQualifiedName,
        field.Name,
        Array.Empty<string>(),
        Array.Empty<string>(),
        field.MemberType)
    {
        Member = field;
        if (field is null) throw new ArgumentNullException(nameof(field));
    }

    public MemberNotation(ConstructorInfo constructor) : this(
        constructor.DeclaringType?.AssemblyQualifiedName,
        constructor.Name,
        AssemblyQualifiedParameterTypes(constructor),
        AssemblyQualifiedGenericArguments(constructor),
        constructor.MemberType)
    {
        Member = constructor;
        if (constructor is null) throw new ArgumentNullException(nameof(constructor));
    }

    public MemberNotation(PropertyInfo property) : this(
        property.DeclaringType?.AssemblyQualifiedName,
        property.Name,
        Array.Empty<string>(),
        Array.Empty<string>(),
        property.MemberType)
    {
        Member = property;
        if (property is null) throw new ArgumentNullException(nameof(property));
    }

    public string Signature
    {
        get
        {
            var declare = Declaring is not null ? Declaring + "." : string.Empty;
            var generic = HasGenerics ? "<" + string.Join(", ", GenericTypes) + ">" : string.Empty;
            var param = HasParameters ? "(" + string.Join(", ", ParameterTypes) + ")" : "()";
            return declare + Name + generic + param;
        }
    }

    public bool HasParameters => ParameterTypes is not null && ParameterTypes.Length > 0;
    public bool HasGenerics => GenericTypes is not null && GenericTypes.Length > 0;

    public MethodInfo ResolveMethod()
    {
        if (MemberType != MemberTypes.Method) throw new InvalidOperationException("Cannot resolve non-Method member to Method");
        if (Member is MethodInfo method) return method;

        var declaring = AccessTools.TypeByName(Declaring) ?? throw new InvalidOperationException();
        var parameters = HasParameters ? ParameterTypes.Select(AccessTools.TypeByName).ToArray() : null;
        var generics = HasGenerics ? GenericTypes.Select(AccessTools.TypeByName).ToArray() : null;

        var result = AccessTools.Method(declaring, Name, parameters, generics);
        Member = result;
        return result;
    }

    public Type ResolveType()
    {
        if (MemberType != MemberTypes.TypeInfo) throw new InvalidOperationException();
        if (Member is Type type) return type;
            
        var result = AccessTools.TypeByName(Name);
        return result;
    }

    public FieldInfo ResolveField()
    {
        if (MemberType != MemberTypes.Field) throw new InvalidOperationException();
        if (Member is FieldInfo field) return field;

        var declaring = AccessTools.TypeByName(Declaring) ?? throw new InvalidOperationException();
        var result = AccessTools.Field(declaring, Name); //enclosing.GetField(Name, AccessTools.allDeclared);

        Member = result;
        return result;
    }

    public ConstructorInfo ResolveConstructor()
    {
        if (MemberType != MemberTypes.Constructor) throw new InvalidOperationException();
        if (Member is ConstructorInfo constructor) return constructor;

        var declaring = AccessTools.TypeByName(Declaring) ?? throw new InvalidOperationException();
        var parameters = HasParameters ? ParameterTypes.Select(AccessTools.TypeByName).ToArray() : null;
            
        var result = AccessTools.Constructor(declaring, parameters);
        Member = result;
        return result;
    }
}