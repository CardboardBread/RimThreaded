using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    // Convenience type for supplying reflectively-accessed method, with implicit operators to allow easy typing.
    public readonly struct MethodNotation
    {
        public static implicit operator MethodNotation(MethodInfo method)
        {
            return new(method);
        }

        public static implicit operator MethodNotation((Type enclosingType, string methodName) pair)
        {
            return new(pair.enclosingType, pair.methodName);
        }

        public static implicit operator MethodNotation(string typeColonName)
        {
            return new(typeColonName);
        }

        public static implicit operator MethodInfo(MethodNotation notation)
        {
            return notation.Method;
        }

        public readonly MethodInfo Method;

        public MethodNotation(MethodInfo method)
        {
            Method = method;
        }

        public MethodNotation(Type enclosingType, string methodName, bool declared = false)
        {
            Method = declared ? AccessTools.DeclaredMethod(enclosingType, methodName) : AccessTools.Method(enclosingType, methodName);
        }

        public MethodNotation(string typeColonName, bool declared = false)
        {
            Method = declared ? AccessTools.DeclaredMethod(typeColonName) : AccessTools.Method(typeColonName);
        }
    }
}
