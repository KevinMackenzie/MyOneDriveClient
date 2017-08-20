using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace Utils
{
    public class ChunkedReadStreamWrapper : Stream
    {
        private readonly Stream _streamImplementation;
        private long _chunkStart;
        private long _chunkSize;
        public ChunkedReadStreamWrapper(Stream internalStream)
        {
            _streamImplementation = internalStream;
        }

        private void UpdateChunk()
        {
            _streamImplementation.Position = _chunkStart;
            if (_chunkStart + _chunkSize > _streamImplementation.Length)
            {
                _chunkSize = _streamImplementation.Length - _chunkStart;
            }
        }

        /// <summary>
        /// Where in the stream the chunk should start
        /// </summary>
        public long ChunkStart
        {
            get { return _chunkStart; }
            set
            {
                _chunkStart = value;
                UpdateChunk();
            }
        }

        public long ChunkSize
        {
            get { return _chunkSize; }
            set
            {
                _chunkSize = value;
                UpdateChunk();
            }
        }

        public long ChunkEnd => ChunkStart + ChunkSize;

        /// <inheritdoc />
        public override void Flush()
        {
            throw new NotSupportedException();
        }
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            long newOffset = 0;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newOffset = ExtMath.Clamp(offset, 0, ChunkSize) + ChunkStart;
                    break;
                case SeekOrigin.Current:
                    newOffset = ExtMath.Clamp(Position + offset, ChunkStart, ChunkEnd) - Position;
                    break;
                case SeekOrigin.End:
                    newOffset = ExtMath.Clamp(offset, -ChunkSize, 0) + ChunkStart;
                    break;
            }
            return _streamImplementation.Seek(newOffset, origin) - ChunkStart;
        }
        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var newCount = (int)ExtMath.Clamp(count, 0, ChunkEnd - Position);
            return _streamImplementation.Read(buffer, offset, newCount);
        }
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            get { return false; }
        }
        /// <inheritdoc />
        public override long Length
        {
            get => ChunkSize;
        }
        /// <inheritdoc />
        public override long Position
        {
            get { return _streamImplementation.Position - ChunkStart; }
            set { _streamImplementation.Position = ExtMath.Clamp(value + ChunkStart, ChunkStart, ChunkEnd); }
        }
    }
}
