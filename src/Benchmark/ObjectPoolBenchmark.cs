using System;
using BenchmarkDotNet.Attributes;
using Libs;

namespace Benchmark
{
    [MemoryDiagnoser]
    public class ObjectPoolBenchmark
    {
        private const int N = 10;
        readonly Person[] _buffer = new Person[N * 10];
        private static readonly ObjectPool<Person> ObjectPool = new ObjectPool<Person>();

        [Benchmark]
        public void ObjectPool_One_thread_10Rents10Returns()
        {

            for (int i = 0; i < N; ++i)
            {
                var p0 = ObjectPool.Rent();
                var p1 = ObjectPool.Rent();
                var p2 = ObjectPool.Rent();
                var p3 = ObjectPool.Rent();
                var p4 = ObjectPool.Rent();
                var p5 = ObjectPool.Rent();
                var p6 = ObjectPool.Rent();
                var p7 = ObjectPool.Rent();
                var p8 = ObjectPool.Rent();
                var p9 = ObjectPool.Rent();

                ObjectPool.Return(p9);
                ObjectPool.Return(p8);
                ObjectPool.Return(p7);
                ObjectPool.Return(p6);
                ObjectPool.Return(p5);
                ObjectPool.Return(p4);
                ObjectPool.Return(p3);
                ObjectPool.Return(p2);
                ObjectPool.Return(p1);
                ObjectPool.Return(p0);
            }
        }

        [Benchmark]
        public void ObjectPool_One_thread_RentsReturns_Loop()
        {
            int count = _buffer.Length;
            int mid = count / 2;

            for (int i = 0; i < count; ++i)
                _buffer[i] = ObjectPool.Rent();

            for (int i = 0; i < mid; ++i)
            {
                //Reorder objects
                ObjectPool.Return(_buffer[i]);
                ObjectPool.Return(_buffer[count - 1 - i]);
            }
        }

    }
}