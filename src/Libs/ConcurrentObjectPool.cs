using System.Threading;

namespace Libs
{
    public class ConcurrentObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        //These numbers can be adjusted according to load profile.

        //Should be power of 2; Than bigger this number than lower contention rate
        //and higher performance in multi-thread load.
        private const uint PoolsCount = 16;

        //The smaller this number, the better memory locality. (slightly better performance).
        //On the other hand, small baskets leads to pool extension that is expensive.
        private const long BasketSize = 100;

        //Limits maximum pool size. Than bigger the number, than bigger memory footprint
        //and more objects can be in use in the same time.
        private const long MaxBasketsCount = 10_000;

        private readonly ObjectPool<T>[] _pools;
        private int _rentCounter;
        private int _returnCounter = (int)(PoolsCount / 2); //Phase shift π. give slight performance boost.

        public ConcurrentObjectPool()
        {
            _pools = new ObjectPool<T>[PoolsCount];
            for (int i = 0; i < PoolsCount; ++i)
            {
                _pools[i] = new ObjectPool<T>(BasketSize, MaxBasketsCount);
            }
        }

        public T Rent()
        {
            unchecked
            {
                uint poolIndex = (uint)Interlocked.Increment(ref _rentCounter) % PoolsCount;
                return _pools[poolIndex].Rent();
            }
        }

        public void Return(T entity)
        {
            unchecked
            {
                uint poolIndex = (uint)Interlocked.Increment(ref _returnCounter) % PoolsCount;
                _pools[poolIndex].Return(entity);
            }

        }
    }
}
