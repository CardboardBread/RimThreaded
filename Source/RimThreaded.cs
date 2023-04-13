using RimThreaded.Utilities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RimThreaded
{
    // Singleton class for handling the normal operation and general utilities of RimThreaded.
    [StaticConstructorOnStartup]
    public static class RimThreaded
    {
        public static readonly RimThreadedTaskScheduler TaskScheduler;
        public static readonly TaskFactory TaskFactory;

        public static readonly Assembly LocalAssembly = typeof(RimThreaded).Assembly;
        public static readonly Module LocalModule = typeof(RimThreaded).Module;
        public static readonly Type[] LocalTypes = AccessTools.GetTypesFromAssembly(LocalAssembly);

        public static int MaxDegreeOfParallelism => RimThreadedSettings.Instance?.MaxThreads ?? SystemInfo.processorCount;
        public static int MaximumConcurrencyLevel => MaxDegreeOfParallelism;

        internal static readonly ParallelOptions ParallelOptions;

        static RimThreaded()
        {
            RimThreaded.TaskFactory = new();
            RimThreaded.TaskScheduler = new(RimThreaded.TaskFactory);
            ParallelOptions = GetParallelOptions(CancellationToken.None);
        }

        // Get all assemblies that directly interface with the game; Ludeon and user mod assemblies.
        public static IEnumerable<Assembly> GameAssemblies()
        {
            yield return typeof(Game).Assembly;

            foreach (var contentPack in LoadedModManager.runningMods)
            {
                foreach (var assembly in contentPack.assemblies.loadedAssemblies)
                {
                    yield return assembly;
                }
            }
        }

        // Get all assemblies that can have member rebinding applied.
        public static IEnumerable<Assembly> RebindingAssemblies()
        {
            // Adapted from RimThreadedHarmony.ApplyFieldReplacements()
            yield return typeof(Game).Assembly;
            yield return AccessTools.TypeByName("VFECore.VFECore")?.Assembly;
            yield return AccessTools.TypeByName("GiddyUp.Mod_GiddyUp")?.Assembly;
            yield return AccessTools.TypeByName("SpeakUp.SpeakUpMod")?.Assembly;
        }

        internal static ParallelOptions GetParallelOptions(CancellationToken token) => new()
        {
            TaskScheduler = RimThreaded.TaskScheduler,
            MaxDegreeOfParallelism = RimThreaded.MaxDegreeOfParallelism,
            CancellationToken = token
        };

        // TODO: parameters are a varargs of a method call turned into an array, using `ldftn` to turn the method call into a
        //       function pointer. The array could be seen as a tuple of pointer, instance and parameters:
        //       (IntPtr fn, object inst, object arg0, object arg1, ...)
        private static void Invoke(params object[] parameters)
        {
            throw new NotImplementedException();
        }

        public static void Invoke(params Action[] actions) => Parallel.Invoke(RimThreaded.ParallelOptions, actions);

        public static void Invoke(CancellationToken token, params Action[] actions)
        {
            Parallel.Invoke(GetParallelOptions(token), actions);
        }

        public static void Invoke(IEnumerable<Action> actions) => Invoke(actions.ToArray());

        public static ParallelLoopResult ForEach<T>(IEnumerable<T> source, Action<T> action, ParallelOptions parallelOptions = null)
        {
            return Parallel.ForEach(source, parallelOptions ?? RimThreaded.ParallelOptions, action);
        }
    }
}
