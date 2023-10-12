using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Assertions;

namespace RimThreaded.Utilities
{
    // Wrapper type for itemizing reflective lookups, such that they can be resolved later on in execution, or saved to file and resolved in a separate process/application/time.
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
            if (notation.MemberType == MemberTypes.Method)
            {
                return notation.ResolveMethod();
            }
            else if (notation.MemberType == MemberTypes.Constructor)
            {
                return notation.ResolveConstructor();
            }
            else
            {
                throw new ArgumentException($"Notation does not describe a type subclassing {nameof(MethodBase)}");
            }
        }

        public static MemberNotation FromAnyMethod(MethodBase methodBase)
        {
            if (methodBase is MethodInfo method)
            {
                return (MemberNotation)method;
            }
            else if (methodBase is ConstructorInfo constructor)
            {
                return (MemberNotation)constructor;
            }
            else
            {
                throw new NotImplementedException($"Unknown/Unsupported {nameof(MethodBase)} subclass");
            }
        }

        internal static string[] AssemblyQualifiedGenericArguments(MethodBase methodBase)
        {
            return methodBase.GetGenericArguments().Select(type => type.AssemblyQualifiedName).ToArray();
        }

        internal static string[] AssemblyQualifiedParameterTypes(MethodBase methodBase)
        {
            return methodBase.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName).ToArray();
        }

        [NonSerialized]
        public MemberInfo Member;

        public MemberNotation(MethodInfo method) : this(
            method.DeclaringType.AssemblyQualifiedName,
            method.Name,
            AssemblyQualifiedParameterTypes(method),
            AssemblyQualifiedGenericArguments(method),
            method.MemberType)
        {
            Member = method;
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }
        }

        public MemberNotation(Type type) : this(
            type.DeclaringType?.AssemblyQualifiedName ?? null,
            type.AssemblyQualifiedName,
            new string[0],
            new string[0],
            type.MemberType)
        {
            Member = type;
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            Assert.IsFalse(type.IsGenericParameter, "Building notations for type parameters of generic methods is not supported");
        }

        public MemberNotation(FieldInfo field) : this(
            field.DeclaringType.AssemblyQualifiedName,
            field.Name,
            new string[0],
            new string[0],
            field.MemberType)
        {
            Member = field;
            if (field is null)
            {
                throw new ArgumentNullException(nameof(field));
            }
        }

        public MemberNotation(ConstructorInfo constructor) : this(
            constructor.DeclaringType.AssemblyQualifiedName,
            constructor.Name,
            AssemblyQualifiedParameterTypes(constructor),
            AssemblyQualifiedGenericArguments(constructor),
            constructor.MemberType)
        {
            Member = constructor;
            if (constructor is null)
            {
                throw new ArgumentNullException(nameof(constructor));
            }
        }

        public MemberNotation(PropertyInfo property) : this(
            property.DeclaringType.AssemblyQualifiedName,
            property.Name,
            new string[0],
            new string[0],
            property.MemberType)
        {
            Member = property;
            if (property is null)
            {
                throw new ArgumentNullException(nameof(property));
            }
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
            Assert.IsTrue(MemberType == MemberTypes.Method);
            if (Member is MethodInfo method)
            {
                return method;
            }

            var enclosing = Type.GetType(Declaring);
            var parameters = ParameterTypes?.Select(Type.GetType).ToArray() ?? new Type[0];
            var result = enclosing.GetMethod(Name, AccessTools.allDeclared, null, parameters, new ParameterModifier[0]);

            if (HasGenerics)
            {
                var generics = GenericTypes.Select(Type.GetType).ToArray();
                result = result.MakeGenericMethod(generics);
            }

            Member = result;
            return result;
        }

        public Type ResolveType()
        {
            Assert.IsTrue(MemberType == MemberTypes.TypeInfo);
            if (Member is Type type)
            {
                return type;
            }

            var result = Type.GetType(Name);
            return result;
        }

        public FieldInfo ResolveField()
        {
            Assert.IsTrue(MemberType == MemberTypes.Field);
            if (Member is FieldInfo field)
            {
                return field;
            }

            var enclosing = Type.GetType(Declaring);
            var result = enclosing.GetField(Name, AccessTools.allDeclared);

            Member = result;
            return result;
        }

        public ConstructorInfo ResolveConstructor()
        {
            Assert.IsTrue(MemberType == MemberTypes.Constructor);
            if (Member is ConstructorInfo constructor)
            {
                return constructor;
            }

            var enclosing = Type.GetType(Declaring);
            var parameters = ParameterTypes?.Select(Type.GetType).ToArray() ?? new Type[0];
            var result = enclosing.GetConstructor(AccessTools.allDeclared, null, parameters, new ParameterModifier[0]);

            Member = result;
            return result;
        }
    }
}
