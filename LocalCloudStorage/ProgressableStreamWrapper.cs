using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage
{
    class ProgressableStreamWrapper : Stream
    {
        private Stream _stream;
        private long _length;
        private long _cumulativeProgress;
        /// <summary>
        /// Creates a new wrapper around a stream to track progress of the read/write.
        /// </summary>
        /// <param name="stream">the stream to track.  Must support <see cref="Stream.Length"/></param>
        public ProgressableStreamWrapper(Stream stream) : this(stream, stream.Length)
        {
        }
        /// <summary>
        /// Creates a new wrapper around a stream to track progress of the item.
        /// </summary>
        /// <param name="stream">the stream to track</param>
        /// <param name="length">the total length of the stream</param>
        public ProgressableStreamWrapper(Stream stream, long length)
        {
            _stream = stream;
            _length = length;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _stream.Flush();
        }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }
        /// <inheritdoc />
        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _stream.Read(buffer, offset, count);

            _cumulativeProgress += count;
            SafeInvokeReadProgressChanged(new ProgressChangedEventArgs(_cumulativeProgress, _length));

            return bytesRead;
        }
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            SafeInvokeWriteProgressChanged(new ProgressChangedEventArgs(offset + count, _length));
        }
        /// <inheritdoc />
        public override bool CanRead => _stream.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => _stream.CanSeek;
        /// <inheritdoc />
        public override bool CanWrite => _stream.CanWrite;
        /// <inheritdoc />
        public override long Length => _stream.Length;
        /// <inheritdoc />
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }


        private void SafeInvokeReadProgressChanged(ProgressChangedEventArgs e)
        {
            OnReadProgressChanged?.Invoke(this, e);
        }
        private void SafeInvokeWriteProgressChanged(ProgressChangedEventArgs e)
        {
            //OnWriteProgressChanged?.Invoke(this, e);
        }

        public event EventDelegates.ProgressChangedHandler OnReadProgressChanged;
        //public event EventDelegates.ProgressChangedHandler OnWriteProgressChanged;
    }
}
