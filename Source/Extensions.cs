using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded
{
    public static class Extensions
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
        {
            return source.Select((item, index) => (item, index));
        }

        public static IEnumerable<MemberInfo> AllMembers(this Assembly assembly)
        {
            return from type in assembly.GetTypes()
                   from member in type.AllMembers()
                   select member;
        }

        public static IEnumerable<MemberInfo> AllMembers(this Type type)
        {
            yield return type;

            foreach (var field in type.GetFields(AccessTools.allDeclared))
            {
                yield return field;
            }

            foreach (var methodBase in type.AllMethodBases())
            {
                yield return methodBase;
            }
        }

        public static IEnumerable<MethodBase> AllMethodBases(this Assembly assembly)
        {
            return from type in assembly.GetTypes()
                   from methodBase in type.AllMethodBases()
                   select methodBase;
        }

        public static IEnumerable<MethodBase> AllMethodBases(this Type type)
        {
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
                if (property.GetMethod is MethodInfo getMethod)
                {
                    yield return getMethod;
                }

                if (property.SetMethod is MethodInfo setMethod)
                {
                    yield return setMethod;
                }
            }
        }
    }
}
