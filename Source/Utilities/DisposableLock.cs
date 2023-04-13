using System;
using System.Threading;

namespace RimThreaded.Utilities
{
    // Using IDisposable to allow standard thread-safe access to arbitrary objects.
    public class DisposableLock<T> : IDisposable where T : class
    {
        public static implicit operator T(DisposableLock<T> dLock) => dLock.Value;

        private readonly object _lockObj;
        private readonly T _value;
        private bool _isDisposed = false;

        public T Value
        {
            get
            {
                if (!_isDisposed)
                {
                    return _value;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public DisposableLock(T value, object lockObj = null)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _lockObj = lockObj ?? value;
            Reset();
        }

        // Attempt to reacquire an exclusive lock on `value`.
        public void Reset()
        {
            if (!_isDisposed)
            {
                throw new InvalidOperationException();
            }

            if (!Monitor.TryEnter(_lockObj))
            {
                throw new InvalidOperationException();
            }
            _isDisposed = false;
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (isDisposing)
            {
            }

            Monitor.Exit(_lockObj);
            _isDisposed = true;
        }

        ~DisposableLock()
        {
            Dispose(isDisposing: false);
        }

        public void Dispose()
        {
            Dispose(isDisposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
