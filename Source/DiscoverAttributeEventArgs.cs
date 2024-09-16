using System;
using System.Reflection;

namespace RimThreaded;

public class DiscoverAttributeEventArgs : EventArgs
{
    public DiscoverAttributeEventArgs(MemberInfo member, object[] attributes)
    {
        Member = member;
        Attributes = attributes;
    }

    public MemberInfo Member { get; }

    public object[] Attributes { get; }
}