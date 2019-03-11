using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Libs
{
    public interface IObjectPool<T> where T : class, new()
    {
        T Rent();
        void Return(T entity);
    }

    /// <summary>
    ///     Lightweight hight performance object pool.
    /// </summary>
    /// <typeparam name="T">Type of pooled object</typeparam>
    public sealed class ObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        /*
         * Logicaly, objects are stored as a  stack. Stack is based on an array,
         * but it is splitted into jagged array because it does not require
         * to allocate all the memory. So, it makes possible to be 'unlimited'
         * and does not consume extra memory if it is not needed.
         * 
         * _head consist of next parts:
         *     Index - 32-bit stack pointer.
         *     Tag   - 31-bit tag to avoid ABA problem.
         *     L     - 1-bit flag represens exclusive write lock
         * _head binary representation:
         * Bytes |_7_|_6_|_5_|_4_|_3_|_2_|_1_|_0_|
         *       |L|     Tag     |     Index     |
         */
        private const int  IndexBits = 32; //Can be used to move border betwean tag and index.
        private const long IndexMask = 0xFFFF_FFFFL; //Low 32-bits
        private const long TagMask   = 0x7FFF_FFFFL << IndexBits; //High 32-bits excluding MST bit
        private const long UnlockMask = TagMask | IndexMask; //Excluding MST bit
        private const long LockMask = ~UnlockMask; //Most significant bit
        private const long TagIncrement = 1L << IndexBits; //Increment tag by one
        private const long MaxIndex = ~(-1L << IndexBits); //Set low IndexBits-bits to 1

        private long _head;
        private readonly ulong _basketSize;
        private readonly long _maxIndex;
        private readonly T[][] _data;

        public ObjectPool(long basketSize = 1000, long maxBasketsCount = 100_000)
        {
            _basketSize = (ulong)basketSize;
            _maxIndex = maxBasketsCount * basketSize - 1;
            if(MaxIndex < _maxIndex) throw new ArgumentException($"Maximum allowed total size (basketSize * maxBasketsCount) is {MaxIndex}.");
            _data = new T[maxBasketsCount][];

            //Fill first basket with new objects at startup.
            //It brings light performance boost due to data locality.
            //Under real load objects will be reordered. So, real impact can be negligible.
            //Leave 1/4 of basket free. It helps to avoid pool extension if there are allocations outsite of pool.
            long preAllocateSize = (long)(_basketSize * 0.750);
            _data[0] = InitBasket(preAllocateSize);
            _head = preAllocateSize - 1;
        }

        public T Rent()
        {
            T result;

            while(true)
            {
                long head = Volatile.Read(ref _head);

                //It might be not obvious, but read is not allowed if write lock taken.
                //Read removes write lock and leads to simultaneous writes.
                if (IsWriteLock(head)) continue;
                long index = head & IndexMask;
                if (index == 0) return new T(); //Slot with index 0 reserved as empty marker

                var pos = Split(index);
                result = Volatile.Read(ref _data[pos.i][pos.j]);
                long newHead = GetNewHead(head - 1, false);
                if (Interlocked.CompareExchange(ref _head, newHead, head) == head) break;
            }

            return result;
        }

        public void Return(T entity)
        {
            while (true)
            {
                long head = Volatile.Read(ref _head);
                if (IsWriteLock(head)) continue;

                long index = head & IndexMask;
                if (index == _maxIndex) return; //Pool is full. Just drain an object.

                var pos = Split(++index);
                EnsureRowInitialized(pos);

                long newHead = GetNewHead(head, true); //Begin transaction
                if (Interlocked.CompareExchange(ref _head, newHead, head) != head) continue;
                head = newHead;

                Volatile.Write(ref _data[pos.i][pos.j], entity);

                newHead = GetNewHead(head + 1, false); //Commit or rollback transaction
                if (Interlocked.CompareExchange(ref _head, newHead, head) == head) break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWriteLock(long head)
        {
            bool isRunningPrePublish = (head & LockMask) == LockMask;
            return isRunningPrePublish;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long GetNewHead(long head, bool @lock)
        {
            unchecked{head += TagIncrement;}
            return @lock ? head | LockMask : head & UnlockMask;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureRowInitialized((long i, long j) pos)
        {
            //Pool extension is uncommon operation. So, lock here does not significantly affect
            //performance but drastically simplified thread synchronization.
            if (Volatile.Read(ref _data[pos.i]) == null)
            {
                lock (_data)
                {
                    if (Volatile.Read(ref _data[pos.i]) == null)
                    {
                        Volatile.Write(ref _data[pos.i], new T[_basketSize]);
                    }
                }
            }
        }

        private T[] InitBasket(long preAllocateSize)
        {
            T[] basket = new T[_basketSize];
            for (long i = 0; i < preAllocateSize; ++i)
                basket[i] = new T();
            return basket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (long i, long j) Split(long index)
        {
            //For perfomance reason it is important to perfom divisions on uint operands
            ulong i = (ulong)index / _basketSize;
            ulong j = (ulong)index % _basketSize;
            return ((long)i, (long)j);
        }

        #region Expose internals to tests
        internal long Index
        {
            get
            {
                long head = Volatile.Read(ref _head);
                return head & IndexMask;
            }
        }

        internal T this[long index]
        {
            get
            {
                var pos = Split(index);
                return Volatile.Read(ref _data[pos.i][pos.j]);
            }
        }
        #endregion
    }

}
