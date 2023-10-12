using System.Reflection;

namespace RimThreaded.Utilities
{
    /// <summary>
    /// Template for attributes that can be indicated of their own location.
    /// </summary>
    public interface ILocationAware
    {
        MemberInfo Parent { get; }
        void Locate(MemberInfo parent);
        bool IsLocated();
    }
}
