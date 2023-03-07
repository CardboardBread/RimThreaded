using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace RimThreaded.StaticReplacement
{
    public class TypeReplacement : MemberReplacement
    {
        public new Type Original;
        public new Type Replacement;
        public TypeBuilder Builder => Replacement as TypeBuilder;
        public override bool IsNew() => Replacement is TypeBuilder;

        public List<MemberReplacement> MemberReplacements = new();
        public IEnumerable<FieldReplacement> FieldReplacements => MemberReplacements.OfType<FieldReplacement>();
        public IEnumerable<MethodReplacement> MethodReplacements => MemberReplacements.OfType<MethodReplacement>();
    }
}
