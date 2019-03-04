using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Libs;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class ObjectPoolTests
    {
        private ObjectPool<TestObject> _objectPool;

        [SetUp]
        public void Setup()
        {
            _objectPool = new ObjectPool<TestObject>();
        }

        [TestCase(10)]
        [Ignore("Debug.")]
        public void Run_Single_thread(int count)
        {
            var objects = new TestObject[count];

            for (int i = 0; i < count; ++i)
                objects[i] = _objectPool.Rent();

            for (int i = 0; i < count; ++i)
                _objectPool.Return(objects[i]);

            for (int i = 0; i < count; ++i)
                objects[i] = _objectPool.Rent();

            for (int i = 0; i < count; ++i)
                _objectPool.Return(objects[i]);
        }

        [TestCase(1000_000, 1)]
        [TestCase(100_000, 2)]
        [TestCase(100_000, 10)]
        [TestCase(100_000, 50)]
        //This test does not give proper guarantee that it is thread safety. However it is better then nothing.
        public async Task Shoul_Run_Correctly_Multy_Thread_Environment(int count, int threadsCount)
        {
            var tasks = new Task[threadsCount];
            ulong initialIndex = _objectPool.Index;

            for (int i = 0; i < threadsCount; ++i)
            {
                tasks[i] = Task.Factory.StartNew(() => RentAndReturn(count), TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(tasks);

            ulong finalIndex = _objectPool.Index;
            Assert.AreEqual(initialIndex, finalIndex);

            var pooledObjects = new HashSet<TestObject>();
            ulong duplicatesCount = 0;

            //All elements before stack pointer have NOT to be NULL and used once.
            for (ulong i = 0; i <= initialIndex; ++i)
            {
                var obj = _objectPool[i];
                Assert.IsNotNull(obj);
                if (pooledObjects.Contains(obj)) duplicatesCount++;
                pooledObjects.Add(obj);
            }

            Assert.AreEqual(0, duplicatesCount);

            //All elements after stack pointer have to be NULL.
            for (ulong i = 1; i <= 100; ++i)
            {
                Assert.IsNull(_objectPool[initialIndex + i]);
            }
    }

        private void RentAndReturn(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                var p0 = _objectPool.Rent();
                var p1 = _objectPool.Rent();
                var p2 = _objectPool.Rent();
                var p3 = _objectPool.Rent();
                var p4 = _objectPool.Rent();
                var p5 = _objectPool.Rent();
                var p6 = _objectPool.Rent();
                var p7 = _objectPool.Rent();
                var p8 = _objectPool.Rent();
                var p9 = _objectPool.Rent();

                _objectPool.Return(p0);
                _objectPool.Return(p1);
                _objectPool.Return(p2);
                _objectPool.Return(p3);
                _objectPool.Return(p4);
                _objectPool.Return(p5);
                _objectPool.Return(p6);
                _objectPool.Return(p7);
                _objectPool.Return(p8);
                _objectPool.Return(p9);
            }
        }

        private class TestObject
        {
            public TestObject()
            {
                Id = new Guid();
            }

            public Guid Id { get; }
        }
    }

}
