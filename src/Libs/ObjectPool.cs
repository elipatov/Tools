using System.Runtime.CompilerServices;
using System.Threading;

namespace Libs
{
    /// <summary>
    ///     Lightweight hight performance object pool.
    /// </summary>
    /// <typeparam name="T">Type of pooled object</typeparam>
    public sealed class ObjectPool<T> where T : class, new()
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
         *     L     - 1-bit flag representing exclusive write lock
         * _head binary representation:
         * Bytes |_7_|_6_|_5_|_4_|_3_|_2_|_1_|_0_|
         *       |L|     Tag     |     Index     |
         */
        private const int   IndexBits = 32;
        private const ulong IndexMask = 0xFFFF_FFFF; //Low 32-bits
        private const ulong TagMask = 0x7FFF_FFFFUL << IndexBits; //High 32-bits excluding MST
        private const ulong LockMask = ~(TagMask | IndexMask); //Most significant bit
        private const ulong TagIncrement = 1UL << IndexBits; //Increment tag by one

        private long _head;
        private readonly ulong _basketSize;
        private readonly ulong _maxIndex;
        private readonly T[][] _data;

        public ObjectPool(int basketSize = 1000, int maxBasketsCount = 100_000)
        {
            _basketSize = (ulong)basketSize;
            _maxIndex = (ulong)(maxBasketsCount * basketSize - 1);
            _data = new T[maxBasketsCount][];

            //Fill first basket with new objects at startup.
            //It brings light performance boost due to data locality.
            //Under real load objects will be reordered. So, real impact can be negligible.
            //Leave 1/4 of basket free. It helps to avoid pool extension if there are allocations outsite of pool.
            ulong preAllocateSize = (ulong) (_basketSize * 0.750);
            _data[0] = InitBasket(preAllocateSize);
            _head = GetNewHead(preAllocateSize - 1, 0, false);
        }

        public T Rent()
        {
            long head;
            long newHead;
            T result;

            do
            {
                head = Volatile.Read(ref _head);
                ulong index = (ulong)head & IndexMask;
                ulong tag = (ulong)head & TagMask;
                if (index == 0) return new T(); //Slot with index 0 reserved as empty marker

                var pos = Split(index);
                result = Volatile.Read(ref _data[pos.i][pos.j]);
                newHead = GetNewHead(--index, tag, false);
            } while (Interlocked.CompareExchange(ref _head, newHead, head) != head);

            return result;
        }

        public void Return(T entity)
        {
            while (true)
            {
                long head = Volatile.Read(ref _head);
                bool isRunningPrePublish = ((ulong)head & LockMask) == LockMask;
                if (isRunningPrePublish) continue;

                ulong index = (ulong)head & IndexMask;
                ulong tag = (ulong)head & TagMask;
                if (index == _maxIndex) return; //Pool is full. Just drain object.

                //Begin transaction
                long newHead = GetNewHead(index, tag, true);
                if (Interlocked.CompareExchange(ref _head, newHead, head) != head) continue;
                head = newHead;

                var pos = Split(++index);
                EnsureRowInitialized(pos);
                Volatile.Write(ref _data[pos.i][pos.j], entity);

                //Commit transaction
                newHead = GetNewHead(index, tag, false);
                if (Interlocked.CompareExchange(ref _head, newHead, head) == head) break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetNewHead(ulong newIndex, ulong tag, bool prePublish)
        {
            ulong head = ((tag + TagIncrement) & TagMask) | (newIndex & IndexMask);
            if (prePublish) head |= LockMask;
            return (long)head;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureRowInitialized((ulong i, ulong j) pos)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T[] InitBasket(ulong preAllocateSize)
        {
            T[] basket = new T[_basketSize];
            for (ulong i = 0; i < preAllocateSize; ++i)
                basket[i] = new T();
            return basket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (ulong i, ulong j) Split(ulong index)
        {
            ulong i = index / _basketSize;
            ulong j = index % _basketSize;
            return (i, j);
        }

        #region Expose internals to tests
        internal ulong Index
        {
            get
            {
                long head = Volatile.Read(ref _head);
                ulong index = (ulong)head & IndexMask;
                return index;
            }
        }

        internal T this[ulong index]
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
