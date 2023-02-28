using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace RimThreaded.Utilities
{
    public static class MethodAttributeUtility
    {
        public static IEnumerable<MethodInfo> GetStaticAttributeTargets<A>() where A : Attribute
        {
            return from type in RimThreadedMod.LocalTypes
                   from method in type.GetMethods(BindingFlags.Static)
                   where method.HasAttribute<A>()
                   select method;
        }

        public static void RunAllAttributeTargets<A>(IEnumerable<MethodInfo> methods, string targetName = null) where A : Attribute
        {
            targetName ??= typeof(A).Name;

            foreach (var target in methods)
            {
                try
                {
                    target.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    if (ex is TargetException)
                    {
                        Log.Error($"{targetName} ({target}) is not static!\n{ex}");
                    }
                    else if (ex is ArgumentException || ex is TargetParameterCountException)
                    {
                        Log.Error($"{targetName} ({target}) has a non-zero number of parameters!\n{ex}");
                    }
                    else
                    {
                        Log.Error($"Encountered Exception trying to invoke {targetName} method!\n{ex}");
                    }
                }
            }
        }
    }
}
