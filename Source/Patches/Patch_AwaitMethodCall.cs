using HarmonyLib;
using MonoMod.Utils;
using RimThreaded.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace RimThreaded.Patches;

// Replace call and virtualcall instructions with `OpCodes.ldftn` to turn method calls into passing a function pointer and arguments to a thread pool.
// TODO: infixes but for method references, to allow putting an expensive call into a background thread without creating a closure.
[HarmonyPatch]
public static class Patch_AwaitMethodCall
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Execute(IEnumerable<CodeInstruction> instructions,
        ILGenerator iLGenerator,
        MethodBase original)
    {
        foreach (var instr in instructions)
        {
            var isCall = instr.opcode == OpCodes.Call;
            var isCallvirt = instr.opcode == OpCodes.Callvirt;
            if ((isCall || isCallvirt) && instr.operand is MethodInfo call)
            {
                var paramCount = call.GetParameters().Length;
                var paramTypes = call.GetParameters().Select(p => p.ParameterType).ToArray();
                var infixTypes = paramTypes.Append(typeof(IntPtr)).ToArray(); // TODO: might need to be all typeof(object) for generic params
                var ftnLoader = isCall ? OpCodes.Ldftn : isCallvirt ? OpCodes.Ldvirtftn : OpCodes.Nop;
                var isReturning = call.ReturnType != typeof(void);
                var infixMethod = AccessTools.Method(typeof(Patch_AwaitMethodCall),
                    isReturning ? nameof(Func_Infix) : nameof(Action_Infix),
                    infixTypes,
                    paramTypes);

                // Original instruction becomes function pointer load and a new call is added to take the same arguments as the original call, plus the function pointer.
                instr.opcode = ftnLoader;
                yield return instr;
                yield return new CodeInstruction(OpCodes.Call, infixMethod);
            }
            else
            {
                yield return instr;
            }
        }
    }

    private static void Action_Infix(IntPtr ptr)
    {
        RimThreaded.TaskFactory.StartNew(Action_Invoke, ptr).Wait();

        static unsafe void Action_Invoke(object state)
        {
            var ptr = (IntPtr)state;
            var ftnPtr = (delegate*<void>)(void*)ptr;
            ftnPtr();
        }
    }

    private static void Action_Infix<TArg0>(TArg0 arg0, IntPtr ptr)
    {
        var state = (arg0, ptr);
        RimThreaded.TaskFactory.StartNew(Action_Invoke<TArg0>, state).Wait();

        static unsafe void Action_Invoke<TArg0>(object state)
        {
            var (arg0, ptr) = ((TArg0, IntPtr))state;
            var ftnPtr = (delegate*<TArg0, void>)(void*)ptr;
            ftnPtr(arg0);
        }
    }

    private static void Action_Infix<TArg0, TArg1>(TArg0 arg0, TArg1 arg1, IntPtr ptr)
    {
        var state = (arg0, arg1, ptr);
        RimThreaded.TaskFactory.StartNew(Action_Invoke<TArg0, TArg1>, state).Wait();

        static unsafe void Action_Invoke<TArg0, TArg1>(object state)
        {
            var (arg0, arg1, ptr) = ((TArg0, TArg1, IntPtr))state;
            var ftnPtr = (delegate*<TArg0, TArg1, void>)(void*)ptr;
            ftnPtr(arg0, arg1);
        }
    }

    private static TResult Func_Infix<TResult>(IntPtr ptr)
    {
        var task = RimThreaded.TaskFactory.StartNew(Func_Invoke<TResult>, ptr);
        task.Wait(RimThreadedSettings.Instance.TimeoutMilliseconds);
        return task.Result;

        static unsafe TResult Func_Invoke<TResult>(object state)
        {
            var ptr = (IntPtr)state;
            var ftnPtr = (delegate*<TResult>)(void*)ptr;
            return ftnPtr();
        }
    }

    private static unsafe TResult Func_Infix<TArg0, TResult>(TArg0 arg0, IntPtr ptr)
    {
        var state = (arg0, ptr);
        var task = RimThreaded.TaskFactory.StartNew(Func_Invoke<TArg0, TResult>, state);
        task.Wait(RimThreadedSettings.Instance.TimeoutMilliseconds);
        return task.Result;

        static unsafe TResult Func_Invoke<TArg0, TResult>(object state)
        {
            var (arg0, ptr) = ((TArg0, IntPtr))state;
            var ftnPtr = (delegate*<TArg0, TResult>)(void*)ptr;
            return ftnPtr(arg0);
        }
    }

    private static unsafe TResult Func_Infix<TArg0, TArg1, TResult>(TArg0 arg0, TArg1 arg1, IntPtr ptr)
    {
        var state = (arg0, arg1, ptr);
        var task = RimThreaded.TaskFactory.StartNew(Func_Invoke<TArg0, TArg1, TResult>, state);
        task.Wait(RimThreadedSettings.Instance.TimeoutMilliseconds);
        return task.Result;

        static unsafe TResult Func_Invoke<TArg0, TArg1, TResult>(object state)
        {
            var (arg0, arg1, ptr) = ((TArg0, TArg1, IntPtr))state;
            var ftnPtr = (delegate*<TArg0, TArg1, TResult>)(void*)ptr;
            return ftnPtr(arg0, arg1);
        }
    }
}