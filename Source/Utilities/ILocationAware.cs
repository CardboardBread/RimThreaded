using System.Reflection;

namespace RimThreaded.Utilities
{
    // Template for attributes that can be indicated of their own location.
    public interface ILocationAware
    {
        MemberInfo Parent { get; }
        void Locate(MemberInfo parent);
        bool IsLocated();
    }
}
