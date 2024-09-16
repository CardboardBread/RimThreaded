using System;
using System.Reflection;

namespace RimThreaded;

public class InstructionScanningEventArgs : EventArgs
{
    public InstructionScanningEventArgs(Assembly assembly, Type declaringType, MethodBase method, HarmonyMethodBody methodBody)
    {
        Assembly = assembly;
        DeclaringType = declaringType;
        Method = method;
        MethodBody = methodBody;
    }

    public Assembly Assembly { get; }
        
    public Type DeclaringType { get; }

    public MethodBase Method { get; }
        
    public HarmonyMethodBody MethodBody { get; }
}