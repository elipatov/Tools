using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Libs
{
    public class ConcurrentObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        //These numbers can be adjusted according to load profile.

        //The smaller this number, the better memory locality. (slightly better performance).
        //On the other hand, small baskets leads to pool extension that is expensive.
        private const long BasketSize = 100;

        //Limits maximum pool size. Than bigger the number, than bigger memory footprint
        //and more objects can be in use in the same time.
        private const long MaxBasketsCount = 10_000;

        private readonly int _minPoolsCount = Environment.ProcessorCount;
        private readonly ThreadLocal<ThreadLocalPool> _locals;
        private readonly Node _head;
        private volatile Node _tail;

        public ConcurrentObjectPool()
        {
            _locals = new ThreadLocal<ThreadLocalPool>(true);
            _head = new Node();
            Node node = _head;

            for (int i = 1; i < _minPoolsCount; ++i)
            {
                node.Next = new Node();
                node = node.Next;
            }
            _tail = node;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ObjectPool<T> GetThreadLocalPool()
        {
            ThreadLocalPool threadLocalPool = _locals.Value;
            if (threadLocalPool == null)
            {
                threadLocalPool = GetUnownedBasket();
                if (threadLocalPool == null)
                {
                    var newTail = new Node(Thread.CurrentThread);
                    threadLocalPool = newTail.ThreadLocalPool;
                    while (Interlocked.CompareExchange(ref _tail.Next, newTail, null) != null);
                    _tail = newTail;

                    //Node tail = _tail;
                    //while (Interlocked.CompareExchange(ref tail.Next, newTail, null) != null) ;
                    //Interlocked.CompareExchange(ref _tail, newTail, tail);
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
            Node node = _head;
            do
            {
                if (node.ThreadLocalPool.TryTakeOwnership()) return node.ThreadLocalPool;
                node = Volatile.Read(ref node.Next);
            } while (node != null);
            return null;
        }

        private class ThreadLocalPool
        {
            private Thread _ownerThread;

            public ThreadLocalPool(Thread ownerThread = null)
            {
                Pool = new ObjectPool<T>(BasketSize, MaxBasketsCount);
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

        private class Node
        {
            public Node(Thread ownerThread = null)
            {
                ThreadLocalPool = new ThreadLocalPool(ownerThread);
            }

            public readonly ThreadLocalPool ThreadLocalPool;
            public Node Next;
        }
    }
}
