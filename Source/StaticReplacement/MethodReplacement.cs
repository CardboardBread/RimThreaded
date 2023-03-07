using System.Reflection.Emit;
using System.Reflection;

namespace RimThreaded.StaticReplacement
{
    public class MethodReplacement : MemberReplacement
    {
        public new MethodInfo Original;
        public new MethodInfo Replacement;
        public MethodBuilder Builder => Replacement as MethodBuilder;
        public override bool IsNew() => Replacement is MethodBuilder;
    }
}
