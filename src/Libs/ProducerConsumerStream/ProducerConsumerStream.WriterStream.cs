using System;
using System.IO;
using System.Threading;

namespace Libs.ProducerConsumerStream
{
    partial class ProducerConsumerStream
    {
        private sealed class WriterStream : Stream
        {
            private readonly ProducerConsumerStream _producerConsumerStream;
            private readonly CancellationTokenSource _cancellation;

            public WriterStream(ProducerConsumerStream producerConsumerStream)
            {
                _producerConsumerStream = producerConsumerStream;
                _cancellation = new CancellationTokenSource();
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => _producerConsumerStream.WriterPosition;
                set => throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                _producerConsumerStream.Write(buffer, offset, count, _cancellation.Token);

            protected override void Dispose(bool disposing)
            {
                if (disposing) _producerConsumerStream.CompleteWriting();
                base.Dispose(disposing);
            }

            public override void Flush()
            {
            }
        }
    }
}

