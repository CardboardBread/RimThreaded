using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Utilities
{
    // Utility for converting method groups into reflective info. Intended for methods without any overloads, as type contracts can't be inferred.
    public static class MethodGroups
    {
        public static MethodInfo AsInfo(this Delegate del) => del.Method;

        public static MethodInfo AsInfo(this Action act) => act.Method;

        public static MethodInfo AsInfo<In>(this Action<In> act) => act.Method;

        public static MethodInfo AsInfo<Out>(this Func<Out> func) => func.Method;

        public static MethodInfo AsInfo<In, Out>(this Func<In, Out> func) => func.Method;

        private static MethodInfo AsMethodInfo<A>(this Expression<A> e)
        {
            return e.Body.NodeType == ExpressionType.Call && e.Body is MethodCallExpression mce ? mce.Method : null;
        }

        private static MemberInfo AsMemberInfo<A, B>(this Expression<Func<A, B>> e)
        {
            return _AsMemberInfo(e.Body, null);
        }

        private static MemberInfo _AsMemberInfo(Expression body, ParameterExpression param)
        {
            if (body.NodeType == ExpressionType.MemberAccess)
            {
                var bodyMemberAccess = (MemberExpression)body;
                return bodyMemberAccess.Member;
            }
            else if (body.NodeType == ExpressionType.Call)
            {
                var bodyMemberAccess = (MethodCallExpression)body;
                return bodyMemberAccess.Method;
            }
            else throw new NotSupportedException();
        }

        public static MethodBase AsBase(this Delegate del) => del.Method;

        public static HarmonyMethod AsHarmony(this Delegate del) => new(del.Method);
    }
}
