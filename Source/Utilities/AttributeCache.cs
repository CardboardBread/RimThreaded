using System;
using System.Collections.Generic;

namespace RimThreaded.Utilities;

// TODO: move GetAllLocalAttributes and others from RimThreadedMod to here
[Obsolete]
public static class AttributeCache<TAttribute> where TAttribute : Attribute
{
    private static List<TAttribute> _localAttributes = new();

    public static IEnumerable<TAttribute> GetLocals() => _localAttributes;
}