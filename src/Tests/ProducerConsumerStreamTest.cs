using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Libs.ProducerConsumerStream;
using NUnit.Framework;
using FluentAssertions;

namespace Tests
{
    [TestFixture]
    public class ProducerConsumerStreamTest
    {
        private Random _rnd;

        [SetUp]
        public void Setup()
        {
            _rnd = new Random();
        }

        [TestCase(40)]
        [TestCase(255)]
        [TestCase(257)]
        [TestCase(4096)]
        [TestCase(16 * 1024)]
        public async Task Should_Copy_Sync(int size)
        {
            for (int i = 0; i < 10; ++i)
            {
                ProducerConsumerStream stream = new ProducerConsumerStream(64 * 1024);
                byte[] original = GenerateTestData(size);
                await StartProducer(size, original, stream.Writer);

                byte[] actual = ToArray(stream.Reader);
                actual.Should().BeEquivalentTo(original);
            }

        }

        [TestCase(40)]
        [TestCase(255)]
        [TestCase(257)]
        [TestCase(16 * 1024)]
        public void Should_Copy_Async(int size)
        {
            for (int i = 0; i < 10; ++i)
            {
                ProducerConsumerStream stream = new ProducerConsumerStream(1024);
                byte[] original = GenerateTestData(size);
                Task producerTask = StartProducer(size, original, stream.Writer);

                byte[] actual = ToArray(stream.Reader);
                actual.Should().BeEquivalentTo(original);
            }
        }

        [Test]
        [Ignore("Should_Not_Deadlock")]
        public async Task Should_Not_Deadlock_On_Contuniation()
        {
            int size = 16 * 1024;
            ProducerConsumerStream stream = new ProducerConsumerStream(1024);
            byte[] original = GenerateTestData(8);
            Task producerTask = StartCopingProducer(size, original, stream.Writer, true);
            Task consumerTask = StartConsumerAsync(stream, 512, true);
            await Task.WhenAll(producerTask, consumerTask);
        }

        [Test]
        [Ignore("Should_Not_Deadlock")]
        public async Task Should_Not_Deadlock_On_Ping_Pong()
        {
            int size = 1024 * 1024 * 1024;
            ProducerConsumerStream stream = new ProducerConsumerStream(512);
            byte[] original = GenerateTestData(300);
            Task producerTask = StartCopingProducer(size, original, stream.Writer, false);
            Task consumerTask = StartConsumerAsync(stream, 4096, false);
            await Task.WhenAll(producerTask, consumerTask);
        }

        private async Task StartProducer(int size, byte[] original, Stream writeStream)
        {
            int position = 0;

            do
            {
                int chunkSize = _rnd.Next(1, Math.Abs(size - position) / 2 + 1) + 4;
                await writeStream.WriteAsync(original, position, chunkSize);
                position += chunkSize;
            } while (position < size);

            writeStream.Dispose();
        }

        private async Task StartCopingProducer(int size, byte[] original, Stream writeStream, bool delay)
        {
            await Task.Yield();
            int position = 0;

            do
            {
                if(delay) await Task.Delay(5);
                await writeStream.WriteAsync(original, 0, original.Length);
                position += original.Length;
            } while (position < size);

            writeStream.Dispose();
        }

        private async Task StartConsumerAsync(ProducerConsumerStream stream, int bufferSize, bool wait)
        {
            await Task.Yield();
            byte[] buffer = new byte[bufferSize];
            int count;

            do
            {
                if(wait) await stream.WaitAvailableBytesAsync(bufferSize, CancellationToken.None);
                count = await stream.Reader.ReadAsync(buffer, 0, bufferSize);
            } while (count == bufferSize);
        }

        private byte[] GenerateTestData(int size)
        {
            byte[] original = new byte[size];
            _rnd.NextBytes(original);
            original[size - 1] = (byte)_rnd.Next(1, 255);
            original[0] = (byte)_rnd.Next(1, 255);
            return original;
        }

        private static byte[] ToArray(Stream stream)
        {
            using (var readStream = new MemoryStream())
            {
                stream.CopyTo(readStream);
                byte[] plain = readStream.ToArray();
                return plain;
            }
        }
    }
}
