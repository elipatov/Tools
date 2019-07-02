using System;
using System.IO;
using System.Threading;

namespace Libs.ProducerConsumerStream
{
    partial class ProducerConsumerStream
    {
        private sealed class ReaderStream : Stream
        {
            private readonly ProducerConsumerStream _producerConsumerStream;
            private readonly CancellationTokenSource _cancellation;

            public ReaderStream(ProducerConsumerStream producerConsumerStream)
            {
                _producerConsumerStream = producerConsumerStream;
                _cancellation = new CancellationTokenSource();
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _producerConsumerStream.WriterPosition;

            public override long Position
            {
                get => _producerConsumerStream.ReaderPosition;
                set => throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) =>
                _producerConsumerStream.ReadAsync(buffer, offset, count, _cancellation.Token);

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override void Flush()
            {
            }
        }
    }
}
