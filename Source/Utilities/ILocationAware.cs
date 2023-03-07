using System.Reflection;

namespace RimThreaded.Utilities
{
    // Template for attributes that can be indicated of their own location.
    public interface ILocationAware
    {
        internal void Locate(MemberInfo member);
    }
}
