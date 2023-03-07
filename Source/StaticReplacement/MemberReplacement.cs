using System.Reflection;

namespace RimThreaded.StaticReplacement
{
    public abstract class MemberReplacement
    {
        public MemberInfo Original;
        public MemberInfo Replacement;
        public MemberReplacement Parent;
        public abstract bool IsNew();
    }
}
