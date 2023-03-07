using System;

namespace RimThreaded.Utilities
{
    // Marker attribute for initializer methods for the 'premade' case in static member replacement.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ThreadStaticInitializerAttribute : Attribute
    {
    }
}
