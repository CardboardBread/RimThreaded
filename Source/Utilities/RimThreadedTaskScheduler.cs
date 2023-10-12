using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimThreaded.Utilities
{
    public class RimThreadedTaskScheduler : TaskScheduler, IDisposable
    {
        private static ThreadLocal<bool> _currentThreadWorking = new(() => false);

        private readonly object _lockObj = new();
        private readonly LinkedList<Task> _tasks = new();
        private int _delegatesRunning = 0;
        private bool _isDisposed = false;

        public RimThreadedTaskScheduler()
        {
            if (SystemInfo.processorCount < 1)
            {
                var field = AccessTools.Field(typeof(SystemInfo), nameof(SystemInfo.processorCount));
                throw new ArgumentOutOfRangeException(field.ToString());
            }
        }

        public override int MaximumConcurrencyLevel => RimThreaded.MaximumConcurrencyLevel;
        
        public bool IsDisposed
        {
            get
            {
                lock (_lockObj) return _isDisposed;
            }
            private set
            {
                lock (_lockObj) _isDisposed = value;
            }
        }

        public int DelegatesRunning
        {
            get
            {
                lock (_lockObj) return _delegatesRunning;
            }
            private set
            {
                lock (_lockObj) _delegatesRunning = value;
            }
        }

        // Lock will be held until the `DisposableLock` is disposed.
        public DisposableLock<LinkedList<Task>> Tasks => new(_tasks, _lockObj);

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException();
            }

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_lockObj, ref lockTaken);
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
                    Monitor.Exit(_lockObj);
                }
            }
        }

        protected override void QueueTask(Task task)
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException();
            }

            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }
            
            lock (_lockObj)
            {
                if (_delegatesRunning < MaximumConcurrencyLevel)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(_DispatchStart, task);
                    _delegatesRunning++;
                }
                else
                {
                    _tasks.AddLast(task);
                }
            }
        }

        protected override bool TryDequeue(Task task)
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException();
            }

            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            lock (_lockObj)
            {
                return _tasks.Remove(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException();
            }

            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (task.IsCompleted)
            {
                throw new InvalidOperationException();
            }
            
            if (!_currentThreadWorking.Value)
            {
                return false;
            }

            if (taskWasPreviouslyQueued)
            {
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

        // System.Threading.WaitCallback
        private void _DispatchStart(object state)
        {
            _currentThreadWorking.Value = true;

            try
            {
                if (state is Task single)
                {
                    base.TryExecuteTask(single);
                }
                else if (state is IEnumerable<Task> multi)
                {
                    var first = multi.First();

                    var remaining = multi.Skip(1);
                    foreach (Task extra in remaining)
                    {
                        QueueTask(extra);
                    }

                    base.TryExecuteTask(first);
                }
                else if (state is Action immediate)
                {
                    var wrap = new Task(immediate);
                    base.TryExecuteTask(wrap);
                }
                else
                {
                    throw new ArgumentException($"RT Worker {Thread.CurrentThread.ManagedThreadId} was not provided supported state object", nameof(state));
                }
            }
            catch (Exception ex)
            {
                RTLog.Error($"RT Worker {Thread.CurrentThread.ManagedThreadId} encountered error while executing task:\n{ex}");
            }
            finally
            {
                if (TryGetTask(out var next))
                {
                    _DispatchStart(next);
                }

                _currentThreadWorking.Value = false;
                DelegatesRunning--;
            }
        }

        // Try to remove the first task in the list.
        protected bool TryGetTask(out Task task)
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException();
            }

            lock (_lockObj)
            {
                var head = _tasks.First;
                if (head == null)
                {
                    task = null;
                    return false;
                }

                task = head.Value;
                _tasks.RemoveFirst();
                return true;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _currentThreadWorking.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                IsDisposed = true;
            }
        }

        // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~RimThreadedTaskScheduler()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
