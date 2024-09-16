using System.Reflection;

namespace RimThreaded.Utilities;

/// <summary>
/// Template for attributes that can be indicated of their own location.
/// </summary>
public interface ILocationAware
{
    /// <summary>
    /// Initializer for informing an attribute to it's declaring member.
    /// </summary>
    /// <param name="parent">The member this attribute is declared on.</param>
    void Locate(MemberInfo parent);

    /// <summary>
    /// Indicator for when an attribute properly possesses its location data.
    /// </summary>
    /// <returns></returns>
    bool IsLocated();
}