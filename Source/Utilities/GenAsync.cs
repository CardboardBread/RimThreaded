using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimThreaded.Utilities
{
    // GenThreading but for async work like System.Threading.Tasks.Task.
    public static class GenAsync
    {
        // Optional count parameter in case values shouldn't be repeatedly accessed.
        private static IEnumerable<IEnumerable<T>> SliceWork<T>(IEnumerable<T> values, int slices, int? count = null)
        {
            count ??= values.Count();
            var perSlice = count / slices;
            var overflow = count % slices;

            for (int i = 0; i < slices; i++)
            {
                // Add one extra item to the first slices.
                yield return values.Take(perSlice + i < overflow ? 1 : 0);
            }
        }

        // Split a collection of items into slices that a set of tasks will be created to execute a callback upon in parallel.
        private static void SlicedForEach<T>(IEnumerable<T> source, Action<T> callback, int? maxSlices = null, int? count = null)
        {
            maxSlices ??= Environment.ProcessorCount;
            var slices = SliceWork(source, maxSlices.Value, count);
            var tasks = slices.Select(itemize).ToArray();
            Task.WaitAll(tasks);

            void execute(IEnumerable<T> slice)
            {
                foreach (var item in slice)
                {
                    try
                    {
                        callback(item);
                    }
                    catch (Exception ex)
                    {
                        RTLog.Error($"Error in {nameof(SlicedForEach)}: {ex}");
                    }
                }
            }

            Task itemize(IEnumerable<T> slice)
            {
                return Task.Run(() => execute(slice));
            }
        }

        // Execute a callback against every element in the given collection, using a queue of worker tasks.
        private static void QueuedForEach<T>(IEnumerable<T> source, Action<T> callback, int? maxWorkers = null)
        {
            // TODO: maybe replace queue with mega-list of tasks.
            maxWorkers ??= Environment.ProcessorCount;
            var queue = new ConcurrentQueue<T>(source);

            var tasks = new List<Task>();
            for (int i = 0; i < maxWorkers; i++)
            {
                tasks.Add(Task.Run(worker));
            }
            var tasksArr = tasks.ToArray();
            Task.WaitAll(tasksArr);

            void worker()
            {
                while (queue.TryDequeue(out var item))
                {
                    callback(item);
                }
            }
        }

        private static void InlineForEach<T>(IEnumerable<T> source, Action<T> callback)
        {
            Task.WaitAll(source.Select(i => Task.Run(() => callback(i))).ToArray());
        }

        private static Task InlineForEachAsync<T>(IEnumerable<T> source, Action<T> callback)
        {
            return Task.WhenAll(source.Select(i => Task.Run(() => callback(i))));
        }
    }
}
