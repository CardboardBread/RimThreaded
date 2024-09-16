using System;
using System.Runtime.Serialization;

namespace RimThreaded.Utilities;

/// <summary>
/// Thrown to indicate an attribute is declared on an incompatible member, beyond the compile-time validation of <see cref="System.AttributeUsageAttribute"/>.
/// </summary>
public class AttributeUsageException : Exception
{
    public AttributeUsageException()
    {
    }

    public AttributeUsageException(string message) : base(message)
    {
    }

    public AttributeUsageException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected AttributeUsageException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}