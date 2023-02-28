using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Utilities
{
    // An attribute to place on a class, that declares a named field within this class should be replaced.
    [AttributeUsage(AttributeTargets.Class)]
    public class ReplaceFieldAttribute : Attribute
    {
        public readonly FieldInfo field;
        private readonly Type[] types;

        public ReplaceFieldAttribute(string typeColonName, bool declared = false, params Type[] types)
        {
            field = declared ? AccessTools.DeclaredField(typeColonName) : AccessTools.Field(typeColonName);
            this.types = types;
        }

        public ReplaceFieldAttribute(Type type, string name, bool declared = false, params Type[] types)
        {
            field = declared ? AccessTools.DeclaredField(type, name) : AccessTools.Field(type, name);
            this.types = types;
        }
    }

    // Same as above attribute, but the provided attribute type will be added to the replacement version.
    public class ReplaceFieldAttribute<A> : ReplaceFieldAttribute where A : Attribute, new()
    {
        public readonly Type attributeType = typeof(A);

        public ReplaceFieldAttribute(string typeColonName, bool declared = false, params Type[] types) : base(typeColonName, declared, types)
        {
        }

        public ReplaceFieldAttribute(Type type, string name, bool declared = false, params Type[] types) : base(type, name, declared, types)
        {
        }
    }
}
