using System;
using System.Threading;
using System.Threading.Tasks;

namespace Libs.Async
{
    public sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Task<IDisposable> _releaser;

        public AsyncLock()
        {
            _semaphore = new SemaphoreSlim(1, 1);
            _releaser = Task.FromResult((IDisposable)new Releaser(_semaphore));
        }

        public async Task<IDisposable> LockAsync(CancellationToken token)
        {
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            return _releaser.Result;
        }

        public IDisposable Lock(CancellationToken token = default(CancellationToken))
        {
            _semaphore.Wait(token);
            return _releaser.Result;
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public Releaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                _semaphore.Release();
            }
        }
    }
}
