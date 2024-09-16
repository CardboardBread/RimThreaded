using HarmonyLib;
using MonoMod.Utils;
using RimThreaded.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RimThreaded.Patching;

/// <summary>
///  HarmonyPatchCategory, but ony any harmony patch target, such that a single patch class can have multiple categories.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Method, AllowMultiple = false)]
public class PatchCategoryAttribute : Attribute
{
    public readonly string Category;

    public PatchCategoryAttribute(string category)
    {
        Category = category;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ReplacePatchSourceAttribute : Attribute, ILocationAware
{
    public delegate (MethodBase, MethodBase) Usage();

    internal MethodInfo _method;
    internal Usage _delegate;

    public void Locate(MemberInfo member)
    {
        _method = (MethodInfo)member;
        _delegate = ((MethodInfo)member).CreateDelegate<Usage>();
    }

    public bool IsLocated()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Marker attribute to declare methods that return data that is applicable for non-attribute replacement patching.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ReplacePatchesSourceAttribute : Attribute, ILocationAware
{
    public delegate IEnumerable<(MethodBase, MethodBase)> Usage_Multiple();

    public delegate (MethodBase, MethodBase) Usage_Single();

    internal MethodInfo _method;
    internal Usage_Multiple _delegate;

    public void Locate(MemberInfo member)
    {
        if (!AccessTools.IsStatic(member))
            throw new ArgumentException($"{typeof(ReplacePatchesSourceAttribute)} usage member must be static");
        _method = (MethodInfo)member;
        _delegate = ((MethodInfo)member).CreateDelegate<Usage_Multiple>();
    }

    public bool IsLocated()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Marker attribute of initialization methods for 'premade' static member replacement.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class ThreadStaticInitializerAttribute : Attribute, ILocationAware
{
    public delegate void Usage();

    internal MethodInfo _method;
    internal Usage _delegate;

    public void Locate(MemberInfo member)
    {
        _method = (MethodInfo)member;
        _delegate = ((MethodInfo)member).CreateDelegate<Usage>();
    }

    public bool IsLocated()
    {
        throw new NotImplementedException();
    }
}