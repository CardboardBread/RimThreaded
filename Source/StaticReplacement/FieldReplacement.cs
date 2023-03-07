using System.Reflection.Emit;
using System.Reflection;

namespace RimThreaded.StaticReplacement
{
    public class FieldReplacement : MemberReplacement
    {
        public new FieldInfo Original;
        public new FieldInfo Replacement;
        public FieldBuilder Builder => Replacement as FieldBuilder;
        public override bool IsNew() => Replacement is FieldBuilder;
    }
}
