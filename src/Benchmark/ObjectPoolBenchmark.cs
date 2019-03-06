using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Libs;
using Libs.Libs;

namespace Benchmark
{
    [MemoryDiagnoser]
    public class ObjectPoolBenchmark
    {
        private const int N = 10;
        readonly Person[] _buffer = new Person[N * 10];
        private static readonly ObjectPool<Person> ObjectPool = new ObjectPool<Person>();
        private static readonly OldPool<Person> OldPool = new OldPool<Person>();

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


        [Benchmark]
        public void OldPool_One_thread_RentsReturns_Loop()
        {
            int count = _buffer.Length;
            int mid = count / 2;

            for (int i = 0; i < count; ++i)
                _buffer[i] = OldPool.Rent();

            for (int i = 0; i < mid; ++i)
            {
                //Reorder objects
                OldPool.Return(_buffer[i]);
                OldPool.Return(_buffer[count - 1 - i]);
            }
        }
    }
}