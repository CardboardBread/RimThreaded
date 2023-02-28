using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Assertions;

namespace RimThreaded.Utilities
{
    /// <summary>
    /// A utility to help make Harmony patches that use locks.
    /// </summary>
    public static class LockTranspilerUtility
    {
        public static MethodInfo InstanceLockWrapperMethod = AccessTools.Method(typeof(LockTranspilerUtility), nameof(WrapMethodInInstanceLock));
        public static HarmonyMethod InstanceLockWrapperTranspiler = new(InstanceLockWrapperMethod);

        /// <summary>
        /// Determines if the provided instruction will transfer control to a scope outside of its enclosing method.
        /// </summary>
        /// 
        /// <param name="instruction">
        /// An instruction from a method, presumably provided from a collection of instructions.
        /// </param>
        /// 
        /// <returns>
        /// If the provided instruction will transfer control to a scope outside of its enclosing method.
        /// </returns>
        public static bool IsExternalReference(CodeInstruction instruction) => instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt;

        // If the given method returns void or something.
        /// <summary>
        /// Determines if a method returns something or void.
        /// </summary>
        /// 
        /// <param name="method">
        /// A method or constructor that could return a type.
        /// </param>
        /// 
        /// <returns>
        /// If a method returns something or void.
        /// </returns>
        public static bool IsMethodReturning(MethodBase method) => method.GetUnderlyingType() != typeof(void);

        /// <summary>
        /// How many IL instructions a method should have at its end for performing a return.
        /// </summary>
        /// 
        /// <param name="method">
        /// A method or constructor.
        /// </param>
        /// 
        /// <returns>
        /// The number of IL instructions for returning from this method.
        /// </returns>
        public static int GetClosingInstructionsCount(MethodBase method) => IsMethodReturning(method) ? 2 : 1;

        // Group a label with the last instructions in the method. Creates a label if none is already present.
        /// <summary>
        /// Retrieves the instructions responsible for returning from a method.
        /// </summary>
        /// 
        /// <param name="method">
        /// The information of the method or constructor.
        /// </param>
        /// 
        /// <param name="originalInstructions">
        /// A collection of all the instructions in the method's body.
        /// </param>
        /// 
        /// <param name="iLGenerator">
        /// A Harmony class for adding and/or modifying properties of the method.
        /// </param>
        /// 
        /// <returns>
        /// A tuple of a label that refers to the beginning of the closing instructions, and a collection of those instructions.
        /// </returns>
        public static (Label, IEnumerable<CodeInstruction>) GetClosingInstructions(MethodBase method, IEnumerable<CodeInstruction> originalInstructions, ILGenerator iLGenerator)
        {
            // Grab the 'return' instruction at the end or the 'load local' and 'return' instructions, if available.
            IEnumerable<CodeInstruction> ending;
            if (IsMethodReturning(method))
            {
                ending = originalInstructions.TakeLast(2);
            }
            else
            {
                ending = originalInstructions.TakeLast(1);
            }

            // Get any label on the first of the ending instructions, or make one if there's none available.
            Label label;
            var topLast = ending.First();
            if (topLast.labels.Any())
            {
                label = topLast.labels.First();
            }
            else
            {
                label = iLGenerator.DefineLabel();
            }

            return (label, ending);
        }

        /// <summary>
        /// Generates two sequences of IL instructions to enter and exit a lock on a object of a given type.
        /// </summary>
        /// 
        /// <param name="lockVarType">
        /// The type of the object to lock on.
        /// </param>
        /// 
        /// <param name="iLGenerator">
        /// A Harmony class for adding and/or modifying properties of a method.
        /// </param>
        /// 
        /// <param name="lockVar">
        /// A representation of a local variable that stores a reference to a target locking object.
        /// Provided as a 'ref' parameter to allow the caller to pass in their own preexisting LocalBuilder or have this method create one.
        /// </param>
        /// 
        /// <param name="lockFlag">
        /// A representation of a local variable that retains the result of using Monitor.Enter().
        /// Provided as a 'ref' parameter to allow the caller to pass in their own preexisting LocalBuilder or have this method create one.
        /// </param>
        /// 
        /// <param name="endLock">
        /// An IL label that (eventually) exists at an instruction at the end of the scope of the lock.
        /// Provided as a nullable 'ref' parameter to allow the caller to pass in their own preexisting Label or have this method create one.
        /// </param>
        /// 
        /// <param name="endLockFinally">
        /// An IL label that (eventually) exists at an instruction at the end of the `finally` block.
        /// Provided as a nullable 'ref' parameter to allow the caller to pass in their own preexisting Label or have this method create one.
        /// This parameter isn't particularly useful to the caller, but is kept just in case it's needed.
        /// </param>
        /// 
        /// <param name="lockObjectLoader">
        /// A sequence of instructions that place the locking object at the top of the evaluation stack, such that it can be stored in `lockVar` and used for locking.
        /// </param>
        /// 
        /// <returns>
        /// A tuple of instructions to enter a lock and instructions to exit a lock, respectively.
        /// </returns>
        public static (IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>) GetLockInstructions(Type lockVarType, ILGenerator iLGenerator, ref LocalBuilder lockVar, ref LocalBuilder lockFlag, ref Label? endLock, ref Label? endLockFinally, IEnumerable<CodeInstruction> lockObjectLoader)
        {
            // Create local variables for locking and tracking the lock's state.
            if (lockVar == null)
            {
                lockVar = iLGenerator.DeclareLocal(lockVarType);
            }
            else
            {
                Assert.IsTrue(lockVar.LocalType == lockVarType);
            }
            if (lockFlag == null)
            {
                lockFlag = iLGenerator.DeclareLocal(typeof(bool));
            }

            // Add labels to the method for exiting the lock's scope.
            if (endLock == null)
            {
                endLock = iLGenerator.DefineLabel();
            }
            if (endLockFinally == null)
            {
                endLockFinally = iLGenerator.DefineLabel();
            }

            // throws InvalidOperationException if the labels aren't made by the time GetExitLockInstructions() is called.
            var enterInstructions = GetEnterLockInstructions(iLGenerator, lockVar, lockVarType, lockFlag, lockObjectLoader);
            var exitInstructions = GetExitLockInstructions(iLGenerator, lockVar, lockVarType, lockFlag, endLock.Value, endLockFinally.Value, lockObjectLoader);
            return (enterInstructions, exitInstructions);
        }

        public static IEnumerable<CodeInstruction> WrapMethodInInstanceLock(IEnumerable<CodeInstruction> originalInstructions, ILGenerator iLGenerator, MethodBase original)
        {
            // The first argument is always the instance.
            var loadInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0)
            };

            // The first argument's type is taken instead of original.DeclaringType, to work with extension methods.
            var instanceType = original.GetParameters()[0].ParameterType;

            return WrapMethodInLock(originalInstructions, iLGenerator, instanceType, loadInstructions, original);
        }

        // If the method returns void, the last instruction should be only 'return'
        // If the method returns anything, the last two instructions should be loading the return value and then returning.
        public static IEnumerable<CodeInstruction> WrapMethodInLock(IEnumerable<CodeInstruction> originalInstructions, ILGenerator iLGenerator, Type lockObjectType, IEnumerable<CodeInstruction> lockObjectLoader, MethodBase original)
        {
            // Get the instructions we need to exit to for intermediate returns, and a label to reference them.
            var (endingLabel, endingInstructions) = GetClosingInstructions(original, originalInstructions, iLGenerator);

            LocalBuilder lockVar = null;
            LocalBuilder lockFlag = null;
            Label? endLock = endingLabel;
            Label? endLockFinally = null;
            var (enterInstructions, exitInstructions) = GetLockInstructions(lockObjectType, iLGenerator, ref lockVar, ref lockFlag, ref endLock, ref endLockFinally, lockObjectLoader);

            // Prepend the method with locking entrance.
            foreach (var instruction in enterInstructions)
            {
                yield return instruction;
            }

            // Get a copy of the method without the ending instructions.
            var methodWithoutEnding = originalInstructions.Take(originalInstructions.Count() - GetClosingInstructionsCount(original));

            // Put the ending-less method after the lock entrance.
            foreach (var instruction in methodWithoutEnding)
            {
                // Replace intermediate returns with lock exits.
                if (instruction.opcode == OpCodes.Ret)
                {
                    // TODO: verify the instruction before the intermediate return is loading the eval stack or storing to the same local variable as the ending uses.
                    instruction.opcode = OpCodes.Leave_S;
                    instruction.operand = endLock;
                }
                else if (instruction.operand is Label labelOperand && labelOperand == endingLabel && instruction.opcode != OpCodes.Leave_S)
                {
                    // TODO: something is referencing the exit label, verify its a jump and the correct state or jump on (jump on true, false, equal, inequal).
                }
                else
                {
                    yield return instruction;
                }
            }

            // Append the ending-less method with the locking exit.
            foreach (var instruction in exitInstructions)
            {
                yield return instruction;
            }

            // Put the ending back on the method.
            foreach (var instruction in endingInstructions)
            {
                yield return instruction;
            }
        }

        // Wrap call instructions by locking on their enclosing type.
        // TODO: wrap every single call in a lock
        // TODO: wrap every call instruction everywhere in locks, or some pre-made set of methods or calls to wrap.
        public static IEnumerable<CodeInstruction> WrapExternalInLock(CodeInstruction callInstruction, ILGenerator iLGenerator)
        {

        }

        // The types for calling Monitor.Enter()
        internal static Type[] EnterMonitorTypes(Type type) => new Type[] { type, typeof(bool).MakeByRefType() };

        // The types for calling Monitor.Exit()
        internal static Type[] ExitMonitorTypes(Type type) => new Type[] { type };

        // lockObjectLoader can be empty if the eval stack is guaranteed to have the lock object on top when these instructions execute
        public static IEnumerable<CodeInstruction> GetEnterLockInstructions(ILGenerator iLGenerator, LocalBuilder lockObjectVar, Type lockObjectType, LocalBuilder lockTakenVar, IEnumerable<CodeInstruction> lockObjectLoader)
        {
            Assert.IsTrue(lockTakenVar.LocalType == typeof(bool));

            // Load the lock object on top the eval stack as provided.
            foreach (var instruction in lockObjectLoader)
            {
                yield return instruction;
            }

            // Store the lock object in our local variable.
            yield return new CodeInstruction(OpCodes.Stloc, lockObjectVar.LocalIndex);

            // Set lockTakenVar to false.
            yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            yield return new CodeInstruction(OpCodes.Stloc, lockTakenVar.LocalIndex);

            // Invoke Monitor.Enter inside a new try block.
            yield return new CodeInstruction(OpCodes.Ldloc, lockObjectVar.LocalIndex)
            {
                blocks = new List<ExceptionBlock>()
                {
                    new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock)
                }
            };
            yield return new CodeInstruction(OpCodes.Ldloca_S, lockTakenVar.LocalIndex); // ref bool
            yield return CodeInstruction.Call(typeof(Monitor), nameof(Monitor.Enter), EnterMonitorTypes(lockObjectType));
#if DEBUG
            // Convention for IL of lock statements under debug.
            yield return new CodeInstruction(OpCodes.Nop);
            yield return new CodeInstruction(OpCodes.Nop);
#endif
        }

        // lockEndLabel should always be on the first instruction after these.
        // lockObjectLoader cannot be empty, as the caller cannot reliably insert instructions as required for loading the lock object.
        public static IEnumerable<CodeInstruction> GetExitLockInstructions(ILGenerator iLGenerator, LocalBuilder lockObjectVar, Type lockObjectType, LocalBuilder lockTakenVar, Label lockEndLabel, Label finallyBlockEndLabel, IEnumerable<CodeInstruction> lockObjectLoader)
        {
            Assert.IsTrue(lockTakenVar.LocalType == typeof(bool));

#if DEBUG
            // Convention for IL of lock statements under debug.
            yield return new CodeInstruction(OpCodes.Nop);
            yield return new CodeInstruction(OpCodes.Nop);
#endif
            // Go to whatever instructions come after the lock.
            yield return new CodeInstruction(OpCodes.Leave_S, lockEndLabel);

            // Create a finally block, conditionally call Monitor.Exit depending on lockTakenVar
            yield return new CodeInstruction(OpCodes.Ldloc)
            {
                blocks = new List<ExceptionBlock>()
                {
                    new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock)
                }
            };

            // Exit the finally block if lockTakenVar == false
            yield return new CodeInstruction(OpCodes.Ldloc, lockTakenVar.LocalIndex);
            yield return new CodeInstruction(OpCodes.Brfalse_S, finallyBlockEndLabel);

            // load the lock object to the eval stack.
            foreach (var instruction in lockObjectLoader)
            {
                yield return instruction;
            }

            // Invoke Monitor.Exit, exit the finally block, and end the try-finally block's scope.
            yield return CodeInstruction.Call(typeof(Monitor), nameof(Monitor.Exit), ExitMonitorTypes(lockObjectType));
            yield return new CodeInstruction(OpCodes.Endfinally)
            {
                labels = new List<Label>()
                {
                    finallyBlockEndLabel
                },
                blocks = new List<ExceptionBlock>()
                {
                    new ExceptionBlock(ExceptionBlockType.EndExceptionBlock)
                }
            };
        }
    }
}
