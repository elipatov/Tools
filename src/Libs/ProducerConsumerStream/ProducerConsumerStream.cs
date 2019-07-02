using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Libs.Async;

namespace Libs.ProducerConsumerStream
{
    public interface IAwaitableStream
    {
        Task WaitAvailableBytesAsync(int size, CancellationToken token);
    }

    /// <summary>
    // Producer Consumer Stream is a collection of bytes with a Stream-based API. Inspired by Stephen Cleary.
    // However it is optimized for high performance: it is lock free and zero allocation so does not create unnecessary memory traffic.
    // It is designed for special load and it is made semi-asynchronous.
    // Read and write operations are long-running and CPU-bound. I do not want to waist threads from thread-pool by making it async.
    // On the other hand consumer requires certain amount of buffered data. It allows to wait asynchronously (non-blocking consumer thread) when buffer is filling.
    /// </summary>
    public sealed partial class ProducerConsumerStream : IAwaitableStream
    {
        // Use a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85000 bytes).
        // Buffers can be long-living, but it is rented from array pool. So do not care it is getting to Gen1, Gen2.
        // It offers a significant improvement in performance.
        private const int MaxSubBufferSize = 81920;

        // Have to be greater then 1K due to array pools perfomance.
        // Bigger values offers better performance in cost of increasing minimum memory footprint. 
        private const int MinSubBufferSize = 32 * 1024;

        private volatile ManualResetEventSlim _canWrite;
        private volatile ManualResetEventSlim _canRead;
        private readonly AsyncConditionVariable _writeSignal;
        private readonly ArrayPool<byte> _bytesPool;
        private readonly ConcurrentQueue<byte[]> _buffers;
        private readonly int _bufferSize;

        private int _bytesAvailabileToRead;
        private int _currentReadBufferPosition;
        private int _currentWriteBufferPosition;
        private int _currentWriteBufferBytesAvailable;
        private byte[] _currentWriteBuffer;
        private volatile bool _writeCompleted;
        private long _writerPosition;
        private long _readerPosition;


        public ProducerConsumerStream(int bufferSize)
        {
            _bytesPool = ArrayPool<byte>.Shared;
            _bufferSize = bufferSize;
            _buffers = new ConcurrentQueue<byte[]>();
            _canWrite = new ManualResetEventSlim(true);
            _canRead = new ManualResetEventSlim(false);
            _writeSignal = new AsyncConditionVariable();
            var readerStream = new ProducerConsumerStream.ReaderStream(this);
            Reader = readerStream;
            Writer = new ProducerConsumerStream.WriterStream(this);
        }

        public Stream Reader { get; }
        public Stream Writer { get; }

        private long WriterPosition => Volatile.Read(ref _writerPosition);
        private long ReaderPosition => Volatile.Read(ref _readerPosition);
        private bool IsEmpty => Volatile.Read(ref _bytesAvailabileToRead) == 0;
        private bool IsFull => Volatile.Read(ref _bytesAvailabileToRead) == _bufferSize;
        private bool _writeWait;

        private void Write(byte[] buffer, int offset, int count, CancellationToken token)
        {
            count = Math.Min(count, buffer.Length - offset);
            while (count > 0)
            {
                _canWrite?.Reset();
                if (IsFull)
                {
                    Volatile.Write(ref _writeWait, true);
                    _canWrite?.Wait(token);
                    Volatile.Write(ref _writeWait, false);
                }

                if (IsFull)
                {
                    _canRead?.Set();
                    continue;
                }

                if (_writeCompleted) throw new OperationCanceledException("Stream has been closed for writing.");
                token.ThrowIfCancellationRequested();

                int availableToWrite = _bufferSize - Volatile.Read(ref _bytesAvailabileToRead);
                int bytesToCopy = Math.Min(count, availableToWrite);
                bytesToCopy = Math.Min(MaxSubBufferSize, bytesToCopy);

                if (_currentWriteBuffer == null)
                {
                    _currentWriteBuffer = _bytesPool.Rent(Math.Max(bytesToCopy, MinSubBufferSize));
                    _currentWriteBufferPosition = 0;
                    _currentWriteBufferBytesAvailable = _currentWriteBuffer.Length;
                    _buffers.Enqueue(_currentWriteBuffer);
                }
                else
                {
                    bytesToCopy = Math.Min(_currentWriteBufferBytesAvailable, bytesToCopy);
                }

                Buffer.BlockCopy(buffer, offset, _currentWriteBuffer, _currentWriteBufferPosition, bytesToCopy);
                _currentWriteBufferBytesAvailable -= bytesToCopy;
                _currentWriteBufferPosition += bytesToCopy;
                Interlocked.Add(ref _bytesAvailabileToRead, bytesToCopy);
                Interlocked.Add(ref _writerPosition, bytesToCopy);
                offset += bytesToCopy;
                count -= bytesToCopy;

                if (_currentWriteBufferBytesAvailable == 0)
                    _currentWriteBuffer = null;

                _canRead?.Set();
                _writeSignal.NotifyAll();
            }
        }

        private int ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            int bytesCopied = 0;
            while (count > 0)
            {
                _canRead?.Reset();
                if (IsEmpty)
                {
                    _canRead?.Wait(token);
                }

                token.ThrowIfCancellationRequested();
                if (IsEmpty)
                {
                    if(_writeCompleted) return bytesCopied;
                    _canWrite?.Set();
                    continue;
                }

                _buffers.TryPeek(out var currentReadBuffer);
                int availableToRead = currentReadBuffer.Length - _currentReadBufferPosition;
                availableToRead = Math.Min(availableToRead, Volatile.Read(ref _bytesAvailabileToRead));
                int bytesToCopy = Math.Min(count, availableToRead);

                Buffer.BlockCopy(currentReadBuffer, _currentReadBufferPosition, buffer, offset, bytesToCopy);
                _currentReadBufferPosition += bytesToCopy;
                Interlocked.Add(ref _bytesAvailabileToRead, -bytesToCopy);
                Interlocked.Add(ref _readerPosition, bytesToCopy);
                offset += bytesToCopy;
                count -= bytesToCopy;
                bytesCopied += bytesToCopy;

                if (_currentReadBufferPosition == currentReadBuffer.Length)
                {
                    _buffers.TryDequeue(out byte[] _);
                    _bytesPool.Return(currentReadBuffer);
                    _currentReadBufferPosition = 0;
                }

                _canWrite?.Set();
            }

            return bytesCopied;
        }

        public async Task WaitAvailableBytesAsync(int size, CancellationToken token)
        {
            while (WriterPosition - ReaderPosition < size && !_writeCompleted)
            {
                await _writeSignal.WaitAsync(token).ConfigureAwait(false);
            }
            
        }

        private void CompleteWriting()
        {
            if (!_writeCompleted)
            {
                _writeCompleted = true;
                ManualResetEventSlim canRead = Interlocked.Exchange(ref _canRead, null);
                ManualResetEventSlim canWrite = Interlocked.Exchange(ref _canWrite, null);
                canRead.Set();
                canWrite.Set();
                canRead.Dispose();
                canWrite.Dispose();
                _writeSignal.Close();
            }
        }
    }
}
