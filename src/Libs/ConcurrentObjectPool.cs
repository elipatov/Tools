using System.Threading;

namespace Libs
{
    public class ConcurrentObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        private const uint PoolsCount = 16;
        private readonly ObjectPool<T>[] _pools;
        private int _rentCounter;
        private int _returnCounter = 8; //Phase shift π. give slight performance boost.

        public ConcurrentObjectPool()
        {
            _pools = new ObjectPool<T>[PoolsCount];
            for (int i = 0; i < PoolsCount; ++i)
            {
                _pools[i] = new ObjectPool<T>(100, 10000);
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
