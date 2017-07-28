using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    public class NotifyDisposedStream : Stream
    {
        private Stream _innerStream;
        public NotifyDisposedStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _innerStream.Flush();
        }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }
        /// <inheritdoc />
        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }
        /// <inheritdoc />
        public override bool CanRead => _innerStream.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => _innerStream.CanSeek;
        /// <inheritdoc />
        public override bool CanWrite => _innerStream.CanWrite;
        /// <inheritdoc />
        public override long Length => _innerStream.Length;
        /// <inheritdoc />
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;
            _innerStream.Dispose();

            //invoke, but DON"T await it.  The other thread may be waiting for this
            //  one to finish disposing
            OnDisposed?.Invoke(this);
        }

        public event EventDelegates.NotifyStreamDisposedHandler OnDisposed;
    }
}
