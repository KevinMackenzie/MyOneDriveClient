using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Encryption
{
    class EncryptedStreamWrapper : Stream
    {
        private Stream _innerStream;
        private IEncryptionProvider _encryptionProvider;

        public EncryptedStreamWrapper(Stream innerStream, IEncryptionProvider provider)
        {
            _innerStream = innerStream;
            _encryptionProvider = provider;
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
            var bytesRead = _innerStream.Read(buffer, offset, count);

            _encryptionProvider.EncryptData(buffer, offset, bytesRead);

            return bytesRead;
        }
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            _encryptionProvider.DecryptData(buffer, offset, count);
            _innerStream.Write(buffer, offset, count);
        }
        /// <inheritdoc />
        public override bool CanRead
        {
            get { return _innerStream.CanRead; }
        }
        /// <inheritdoc />
        public override bool CanSeek
        {
            get { return _innerStream.CanSeek; }
        }
        /// <inheritdoc />
        public override bool CanWrite
        {
            get { return _innerStream.CanWrite; }
        }
        /// <inheritdoc />
        public override long Length
        {
            get { return _innerStream.Length; }
        }
        /// <inheritdoc />
        public override long Position
        {
            get { return _innerStream.Position; }
            set { _innerStream.Position = value; }
        }
    }
}
