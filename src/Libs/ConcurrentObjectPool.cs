using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ThreadState = System.Threading.ThreadState;

namespace Libs
{
    public class ConcurrentObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        //These numbers can be adjusted according to load profile.

        //The smaller this number, the better memory locality. (slightly better performance).
        //On the other hand, small baskets leads to pool extension that is expensive.
        //Making it greathe than 85K moves pool to LOH. It can be benefitial (as far as pool lives as application).
        private const long BasketSize = 11_000;

        //Limits maximum pool size. Than bigger the number, than bigger memory footprint
        //and more objects can be in use in the same time.
        private const long MaxBasketsCount = 10;

        //Use to preallocate part of pool. The bigger the number, the bigger initialization time memory footprint.
        //On the other hand it gives better memory locality and avoids allocations at runtime.
        private const long PreAllocateSize = 1_000;

        private const int PoolsPerProcessor = 4;
        private readonly int _poolsCount = PoolsPerProcessor * Environment.ProcessorCount;
        private readonly ThreadLocal<ThreadLocalPool> _locals;
        private readonly ThreadLocalPool[] _pools;
        private int _index;

        public ConcurrentObjectPool()
        {
            _locals = new ThreadLocal<ThreadLocalPool>(true);
            _pools = new ThreadLocalPool[_poolsCount];
            Init();
        }

        public T Rent()
        {
            ObjectPool<T> pool = GetThreadLocalPool();
            return pool.Rent();
        }

        public void Return(T entity)
        {
            ObjectPool<T> pool = GetThreadLocalPool();
            pool.Return(entity);
        }

        //There is a heap per processor. Distributes caches by processors.
        private void Init()
        {
            // Ensure managed thread is linked to OS thread; does nothing on default host in current .Net versions
            Thread.BeginThreadAffinity();

#pragma warning disable 618
            // BeginThreadAffinity call guarantees stable results for GetCurrentThreadId
            int osThreadId = AppDomain.GetCurrentThreadId();
#pragma warning restore 618

            Process proc = Process.GetCurrentProcess();
            ProcessThread thread = proc.Threads.Cast<ProcessThread>().Single(t => t.Id == osThreadId);

            for (int p = 0; p < Environment.ProcessorCount; ++p)
            {
                thread.ProcessorAffinity = new IntPtr(1 << p);
                int offset = p * PoolsPerProcessor;

                for (int i = 0; i < PoolsPerProcessor; ++i)
                {
                    _pools[offset + i] = new ThreadLocalPool(null);
                }
            }

            long affinityMask = ~(-1L << Environment.ProcessorCount);//Allow all processors
            thread.ProcessorAffinity = new IntPtr(affinityMask);
            Thread.EndThreadAffinity();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ObjectPool<T> GetThreadLocalPool()
        {
            ThreadLocalPool threadLocalPool = _locals.Value;
            if (threadLocalPool == null)
            {
                threadLocalPool = GetUnownedBasket();
                if (threadLocalPool == null)
                {
                    uint index = (uint)Interlocked.Increment(ref _index) % (uint)_poolsCount;
                    threadLocalPool = _pools[index];
                }

                _locals.Value = threadLocalPool;
            }
            return threadLocalPool.Pool;
        }

        /// <summary>
        /// Try to reuse an unowned pool if exist.
        /// Unowned pools are the pools that their owner threads are aborted or terminated.
        /// This is workaround to avoid memory leaks.
        /// </summary>
        private ThreadLocalPool GetUnownedBasket()
        {
            int lruIndex = Volatile.Read(ref _index);
            for (int i = lruIndex; i < _poolsCount; ++i)
            {
                var pool = _pools[i];
                if (pool.TryTakeOwnership()) return pool;
            }

            for (int i = 0; i < lruIndex; ++i)
            {
                ThreadLocalPool pool = _pools[i];
                if (pool.TryTakeOwnership()) return pool;
            }
            return null;
        }

        private class ThreadLocalPool
        {
            private Thread _ownerThread;

            public ThreadLocalPool(Thread ownerThread)
            {
                Pool = new ObjectPool<T>(BasketSize, MaxBasketsCount, PreAllocateSize);
                _ownerThread = ownerThread;
            }

            public readonly ObjectPool<T> Pool;

            public bool TryTakeOwnership()
            {
                Thread owner = Volatile.Read(ref _ownerThread);
                if (owner == null || owner.ThreadState == ThreadState.Stopped)
                {
                    return Interlocked.CompareExchange(ref _ownerThread, Thread.CurrentThread, owner) == owner;
                }
                return false;
            }
        }
    }
}
