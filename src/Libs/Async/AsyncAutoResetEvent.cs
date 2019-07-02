using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Libs.Async
{
    public class AsyncAutoResetEvent
    {
        private const int TRUE = 1;
        private const int FALSE = 0;
        private bool _isClosed;
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _waiters;
        private int _signaled;

        /// <summary>
        /// Creates an async-compatible auto-reset event.
        /// </summary>
        /// <param name="set">Whether the Auto-reset event is initially set or unset.</param>
        public AsyncAutoResetEvent(bool set)
        {
            _waiters = new ConcurrentQueue<TaskCompletionSource<bool>>();
            _signaled = FALSE;
            if (set) Set();
        }

        /// <summary>
        /// Creates an async-compatible auto-reset event that is initially unset.
        /// </summary>
        public AsyncAutoResetEvent()
            : this(false)
        {
        }

        public async Task WaitAsync(CancellationToken token)
        {
            bool signaled = Interlocked.CompareExchange(ref _signaled, FALSE, TRUE) != FALSE;
            if (signaled || Volatile.Read(ref _isClosed)) return;

            var completion = new TaskCompletionSource<bool>();
            _waiters.Enqueue(completion);
            using (token.Register(() => { completion.TrySetCanceled(token); }, false))
            {
                await completion.Task.ConfigureAwait(false);
            }
        }

        public void Set()
        {
            if (_waiters.TryDequeue(out var toRelease))
            {
                Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                    toRelease, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
                toRelease.Task.Wait();
            }
            else
            {
                Volatile.Write(ref _signaled, TRUE);
            }
        }

        public void Close()
        {
            Volatile.Write(ref _isClosed, true);
            while (_waiters.TryDequeue(out var toRelease))
            {
                Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                    toRelease, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
                toRelease.Task.Wait();
            }
        }
    }
}
