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
            RimThreaded.TaskScheduler = new();
            RimThreaded.TaskFactory = new(RimThreaded.TaskScheduler);
            ParallelOptions = GetParallelOptions(CancellationToken.None);
        }

        /// <summary>
        /// Get all assemblies that directly interface with the game; Ludeon and user mod assemblies.
        /// </summary>
        public static IEnumerable<Assembly> GameAssemblies()
        {
            yield return typeof(Game).Assembly;
            foreach (var assembly in from contentPack in LoadedModManager.runningMods
                                     from assembly in contentPack.assemblies.loadedAssemblies
                                     select assembly)
            {
                yield return assembly;
            }
        }

        /// <summary>
        /// Get all assemblies that can have member rebinding applied.
        /// </summary>
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
