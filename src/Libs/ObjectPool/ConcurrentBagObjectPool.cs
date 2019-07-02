using System.Collections.Concurrent;

namespace Libs
{
    public class ConcurrentBagObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentBag<T> _bag;

        public ConcurrentBagObjectPool()
        {
            _bag = new ConcurrentBag<T>();
            for (int i = 0; i < 1200; ++i)
            {
                _bag.Add(new T());
            }
        }

        public T Rent()
        {
            return _bag.TryTake(out T result) ? result : new T();
        }

        public void Return(T entity)
        {
           _bag.Add(entity);
        }
    }
}
