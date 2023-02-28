using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RimThreaded.Utilities
{
    public class RimThreadedTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadWorking;

        // The list of tasks to be executed.
        private readonly LinkedList<Task> _tasks = new();

        // Indicates whether the scheduler is currently processing work items.
        private int _delegatesHandled = 0;

        public RimThreadedTaskScheduler()
        {
            ThreadCount = SystemInfo.processorCount;
            if (ThreadCount < 1) throw new ArgumentOutOfRangeException("UnityEngine reported processor count is unusable!");
        }

        public int ThreadCount { get; set; }

        // Gets the maximum concurrency level supported by this scheduler.
        public override int MaximumConcurrencyLevel => ThreadCount;

        // Gets an enumerable of the tasks currently scheduled on this scheduler.
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken)
                {
                    return _tasks;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_tasks);
                }
            }
        }

        // Queues a task to the scheduler.
        protected override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough
            // delegates currently queued or running to process tasks, schedule another.
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesHandled < ThreadCount)
                {
                    _delegatesHandled++;
                    SignalThreadPool();
                }
            }
        }

        // Attempt to remove a previously scheduled task from the scheduler.
        protected override bool TryDequeue(Task task)
        {
            lock ( _tasks)
            {
                return _tasks.Remove(task);
            }
        }

        // Attempts to execute the specified task on the current thread.
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining.
            if (!_currentThreadWorking)
            {
                return false;
            }

            // If the task was previously queued, remove it from the queue.
            if (taskWasPreviouslyQueued)
            {
                // Try to run the task.
                if (TryDequeue(task))
                {
                    return base.TryExecuteTask(task);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return base.TryExecuteTask(task);
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler.
        private void SignalThreadPool()
        {
            ThreadPool.UnsafeQueueUserWorkItem(obj =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadWorking = true;
                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            if (!_tasks.Any())
                            {
                                _delegatesHandled--;
                                break;
                            }

                            // Get the next item from the queue.
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue.
                        base.TryExecuteTask(item);
                    }
                }
                finally
                {
                    // We're done processing items on the current thread.
                    _currentThreadWorking = false;
                }
            }, this);
        }
    }
}
