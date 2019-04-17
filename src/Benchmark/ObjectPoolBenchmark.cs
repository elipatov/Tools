using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Libs;


namespace Benchmark
{
    [MemoryDiagnoser]
    public class ObjectPoolBenchmark
    {
        private const int N = 10;
        private static readonly ObjectPool<Person> ObjectPool = new ObjectPool<Person>();
        private const int Iterations = 1000_000;

        [Benchmark]
        public void ConcurrentObjectPool()
        {
            Run(new ConcurrentObjectPool<Person>(), 8);
        }

        [Benchmark]
        public void ConcurrentBagObjectPool()
        {
            Run(new ConcurrentBagObjectPool<Person>(), 8);
        }

        private void Run<T>(IObjectPool<T> pool, int threadsCount) where T : class, new()
        {
            var opt = new ParallelOptions { MaxDegreeOfParallelism = threadsCount };
            Parallel.For(0, Iterations, opt, n =>
            {
                var p0 = pool.Rent();
                var p1 = pool.Rent();
                var p2 = pool.Rent();
                var p3 = pool.Rent();
                var p4 = pool.Rent();
                var p5 = pool.Rent();
                var p6 = pool.Rent();
                var p7 = pool.Rent();
                var p8 = pool.Rent();
                var p9 = pool.Rent();

                pool.Return(p0);
                pool.Return(p1);
                pool.Return(p2);
                pool.Return(p3);
                pool.Return(p4);
                pool.Return(p5);
                pool.Return(p6);
                pool.Return(p7);
                pool.Return(p8);
                pool.Return(p9);
            });
        }

        //[Benchmark]
        //public void ObjectPool_100_RentAndReturn_One_Thread()
        //{

        //    for (int i = 0; i < N; ++i)
        //    {
        //        var p0 = ObjectPool.Rent();
        //        var p1 = ObjectPool.Rent();
        //        var p2 = ObjectPool.Rent();
        //        var p3 = ObjectPool.Rent();
        //        var p4 = ObjectPool.Rent();
        //        var p5 = ObjectPool.Rent();
        //        var p6 = ObjectPool.Rent();
        //        var p7 = ObjectPool.Rent();
        //        var p8 = ObjectPool.Rent();
        //        var p9 = ObjectPool.Rent();

        //        ObjectPool.Return(p0);
        //        ObjectPool.Return(p1);
        //        ObjectPool.Return(p2);
        //        ObjectPool.Return(p3);
        //        ObjectPool.Return(p4);
        //        ObjectPool.Return(p5);
        //        ObjectPool.Return(p6);
        //        ObjectPool.Return(p7);
        //        ObjectPool.Return(p8);
        //        ObjectPool.Return(p9);
        //    }
        //}

        //[Benchmark]
        //public void New_100_One_Thread()
        //{
        //    Person p9 = null;
        //    for (int i = 0; i < N; ++i)
        //    {
        //        var p0 = new Person(p9);
        //        var p1 = new Person(p0);
        //        var p2 = new Person(p1);
        //        var p3 = new Person(p2);
        //        var p4 = new Person(p3);
        //        var p5 = new Person(p4);
        //        var p6 = new Person(p5);
        //        var p7 = new Person(p6);
        //        var p8 = new Person(p7);
        //        p9 = new Person(p8);
        //    }
        //}
    }
}