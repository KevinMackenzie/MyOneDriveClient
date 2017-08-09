using System.IO;
using System.Threading;

namespace LocalCloudStorage
{
    public class CancellableStream : Stream
    {
        private Stream _streamImplementation;
        private CancellationToken _ct;
        public CancellableStream(Stream stream, CancellationToken ct)
        {
            _streamImplementation = stream;
            _ct = ct;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _streamImplementation.Flush();
        }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _streamImplementation.Seek(offset, origin);
        }
        /// <inheritdoc />
        public override void SetLength(long value)
        {
            _streamImplementation.SetLength(value);
        }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            _ct.ThrowIfCancellationRequested();
            return _streamImplementation.Read(buffer, offset, count);
        }
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            _ct.ThrowIfCancellationRequested();
            _streamImplementation.Write(buffer, offset, count);
        }
        /// <inheritdoc />
        public override bool CanRead
        {
            get { return _streamImplementation.CanRead; }
        }
        /// <inheritdoc />
        public override bool CanSeek
        {
            get { return _streamImplementation.CanSeek; }
        }
        /// <inheritdoc />
        public override bool CanWrite
        {
            get { return _streamImplementation.CanWrite; }
        }
        /// <inheritdoc />
        public override long Length
        {
            get { return _streamImplementation.Length; }
        }
        /// <inheritdoc />
        public override long Position
        {
            get { return _streamImplementation.Position; }
            set { _streamImplementation.Position = value; }
        }
    }
}
