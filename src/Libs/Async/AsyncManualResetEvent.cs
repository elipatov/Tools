using System.Threading;
using System.Threading.Tasks;

namespace Libs.Async
{
    public class AsyncManualResetEvent
    {
        private const int TRUE = 1;
        private const int FALSE = 0;
        private TaskCompletionSource<bool> _completion;
        private bool _isClosed;
        private int _signaled;

        /// <summary>
        /// Creates an async-compatible manual-reset event.
        /// </summary>
        /// <param name="signalled">Whether the manual-reset event is initially set or unset.</param>
        public AsyncManualResetEvent(bool signalled)
        {
            _completion = new TaskCompletionSource<bool>();
            _signaled = signalled ? TRUE : FALSE;
            if(signalled) Set();
        }

        /// <summary>
        /// Creates an async-compatible manual-reset event that is initially unset.
        /// </summary>
        public AsyncManualResetEvent()
            : this(false)
        {
        }

        public async Task WaitAsync(CancellationToken token)
        {
            TaskCompletionSource<bool> completion = Volatile.Read(ref _completion);
            if (completion == null) return;

            using (token.Register(() => { completion.TrySetCanceled(token); }, false))
            {
                await completion.Task.ConfigureAwait(false);
            }
        }

        public void Set()
        {
            Volatile.Write(ref _signaled, TRUE);
            TaskCompletionSource<bool> completion = Volatile.Read(ref _completion);
            Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                completion, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
            completion.Task.Wait();
        }

        public void Reset()
        {
            if (Volatile.Read(ref _isClosed)) return;
            bool wasSignaled = Interlocked.Exchange(ref _signaled, FALSE) != FALSE;
            if (wasSignaled)
            {
                Volatile.Write(ref _completion, new TaskCompletionSource<bool>());
            }
        }

        public void Close()
        {
            Volatile.Write(ref _isClosed, true);
            Set();
        }
    }
}
