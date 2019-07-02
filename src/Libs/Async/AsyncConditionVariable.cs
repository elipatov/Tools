using System.Threading;
using System.Threading.Tasks;

namespace Libs.Async
{
    public class AsyncConditionVariable
    {
        private const int TRUE = 1;
        private const int FALSE = 0;
        private TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>();
        private int _hasAwaiters;
        private bool _isClosed;

        public void NotifyAll()
        {
            bool hasAwaiters = Interlocked.CompareExchange(ref _hasAwaiters, FALSE, TRUE) != FALSE;
            if (hasAwaiters)
            {
                var completion = Volatile.Read(ref _isClosed) 
                    ? Volatile.Read(ref _completion) 
                    : Interlocked.Exchange(ref _completion, new TaskCompletionSource<bool>());

                if (!completion.Task.IsCompleted)
                {
                    Task.Factory.StartNew(s => ((TaskCompletionSource<bool>) s).TrySetResult(true),
                        completion, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
                    completion.Task.Wait();
                }
            }
        }

        public async Task WaitAsync(CancellationToken token)
        {
            Volatile.Write(ref _hasAwaiters, TRUE);
            TaskCompletionSource<bool> completion = Volatile.Read(ref _completion);
            using (token.Register(() => { completion.TrySetCanceled(token); }, false))
            {
                await completion.Task.ConfigureAwait(false);
            }
        }

        public void Close()
        {
            Volatile.Write(ref _isClosed, true);
            NotifyAll();
        }
    }
}
