using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Libs;
using NUnit.Framework;

namespace Tests
{
    //This test does not give proper guarantee that it is thread safety. However it is better then nothing.
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
        public void Run(int count)
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
        [TestCase(500_000, 2)]
        [TestCase(100_000, 10)]
        [TestCase(20_000, 50)]
        public async Task Rent_Runs_Correctly_In_Multy_Thread_Environment(int count, int threadsCount)
        {
            var tasks = new Task[threadsCount];
            var objectsByThreads = new HashSet<TestObject>[threadsCount];

            for (int i = 0; i < threadsCount; ++i)
            {
                HashSet<TestObject> threadStorage = new HashSet<TestObject>();
                objectsByThreads[i] = threadStorage;
                tasks[i] = Task.Factory.StartNew(() => Rent(count, threadStorage), TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(tasks);

            long collisionCount = 0;

            for (int i = 0; i < threadsCount; ++i)
            {
                HashSet<TestObject> threadStorageI = objectsByThreads[i];
                for (int j = i + 1; j < threadsCount; ++j)
                {
                    HashSet<TestObject> threadStorageJ = objectsByThreads[j];
                    foreach (TestObject objI in threadStorageI)
                    {
                        if (threadStorageJ.Contains(objI)) ++collisionCount;
                    }
                }
            }

            Assert.AreEqual(0, collisionCount);

        }

        [TestCase(1000_000, 1)]
        [TestCase(500_000, 2)]
        [TestCase(100_000, 10)]
        [TestCase(20_000, 50)]
        public async Task Return_Runs_Correctly_In_Multy_Thread_Environment(int count, int threadsCount)
        {
            var tasks = new Task[threadsCount];
            long expectedIndex = _objectPool.Index + count*threadsCount*10;
            var objectsByThreads = new HashSet<TestObject>[threadsCount];

            for (int i = 0; i < threadsCount; ++i)
            {
                HashSet<TestObject> threadStorage = new HashSet<TestObject>();
                objectsByThreads[i] = threadStorage;
                tasks[i] = Task.Factory.StartNew(() => Return(count, threadStorage), TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(tasks);

            long finalIndex = _objectPool.Index;
            Assert.AreEqual(expectedIndex, finalIndex);

            var pooledObjects = new HashSet<TestObject>();

            //All elements before stack pointer have NOT to be NULL and used once.
            for (long i = 1; i <= expectedIndex; ++i)
            {
                var obj = _objectPool[i];
                Assert.IsNotNull(obj);
                pooledObjects.Add(obj);
            }

            Assert.AreEqual(expectedIndex, pooledObjects.Count);
        }

        [TestCase(1000_000, 1)]
        [TestCase(500_000, 2)]
        [TestCase(100_000, 10)]
        [TestCase(20_000, 50)]
        public async Task RentAndReturn_Runs_Correctly_In_Multy_Thread_Environment(int count, int threadsCount)
        {
            var tasks = new Task[threadsCount];
            long initialIndex = _objectPool.Index;

            for (int i = 0; i < threadsCount; ++i)
            {
                tasks[i] = Task.Factory.StartNew(() => RentAndReturn(count), TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(tasks);

            long finalIndex = _objectPool.Index;
            Assert.AreEqual(initialIndex, finalIndex);

            var pooledObjects = new HashSet<TestObject>();

            //All elements before stack pointer have NOT to be NULL and used once.
            for (long i = 1; i <= initialIndex; ++i)
            {
                var obj = _objectPool[i];
                Assert.IsNotNull(obj);
                pooledObjects.Add(obj);
            }

            Assert.AreEqual(initialIndex, pooledObjects.Count);

            //All elements after stack pointer have to be NULL.
            for (long i = 1; i <= 100; ++i)
            {
                Assert.IsNull(_objectPool[initialIndex + i]);
            }
        }

        private void Rent(int count, HashSet<TestObject> threadStorage)
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

                threadStorage.Add(p0);
                threadStorage.Add(p1);
                threadStorage.Add(p2);
                threadStorage.Add(p3);
                threadStorage.Add(p4);
                threadStorage.Add(p5);
                threadStorage.Add(p6);
                threadStorage.Add(p7);
                threadStorage.Add(p8);
                threadStorage.Add(p9);
            }
        }

        private void Return(int count, HashSet<TestObject> threadStorage)
        {
            for (int i = 0; i < count; ++i)
            {
                var p0 = new TestObject();
                var p1 = new TestObject();
                var p2 = new TestObject();
                var p3 = new TestObject();
                var p4 = new TestObject();
                var p5 = new TestObject();
                var p6 = new TestObject();
                var p7 = new TestObject();
                var p8 = new TestObject();
                var p9 = new TestObject();

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

                threadStorage.Add(p0);
                threadStorage.Add(p1);
                threadStorage.Add(p2);
                threadStorage.Add(p3);
                threadStorage.Add(p4);
                threadStorage.Add(p5);
                threadStorage.Add(p6);
                threadStorage.Add(p7);
                threadStorage.Add(p8);
                threadStorage.Add(p9);
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
