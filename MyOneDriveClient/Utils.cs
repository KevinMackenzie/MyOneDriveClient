using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public static class Utils
    {
        public static Stream ToStream(this string str, Encoding encoding)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream, encoding);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Copies one stream to another stream in a safe, asynchronous way
        /// </summary>
        /// <param name="source">the source stream to read from</param>
        /// <param name="destination">the destination stream to write to</param>
        /// <param name="chunkSize">the size of the chunks to buffer</param>
        /// <returns></returns>
        /// <remarks>
        /// All stream disposing responsibilities are still on the user of this method
        /// </remarks>
        public static async Task CopyToStreamAsync(this Stream source, Stream destination, int chunkSize = 4096)
        {
            //parameter checks
            if (source == null)
                throw new ArgumentNullException("source", "Source stream must not be null");
            if (destination == null)
                throw new ArgumentNullException("destination", "Destination stream must not be null");
            if (!source.CanRead)
                throw new ArgumentException("Source stream must support reading", "source");
            if (!destination.CanWrite)
                throw new ArgumentException("Destination stream must support writing", "destination");

            if (chunkSize < 1)
                throw new ArgumentException("Buffer size for stream copying must be more than 1", "chunkSize");

            /*
             * 
             * Source: https://psycodedeveloper.wordpress.com/2013/04/04/reliably-asynchronously-reading-and-writing-binary-streams-in-c-always-check-method-call-return-values/
             * 
             */

            /* The source stream may not support seeking; e.g. a stream
             * returned by ZipArchiveEntry.Open() or a network stream. */
            var size = chunkSize;
            var canSeek = source.CanSeek;

            if (canSeek)
            {
                try
                {
                    size = Convert.ToInt32(Math.Min(chunkSize, source.Length));
                }
                catch (NotSupportedException) { canSeek = false; }
            }

            var buffer = new byte[size];
            var remaining = canSeek ? source.Length : 0;

            /* If the stream is seekable, seek through it until all bytes are read.
             * If we read less than the expected number of bytes, it indicates an
             * error, so throw the appropriate exception.
             *
             * If the stream is not seekable, loop until we read 0 bytes. (It’s not
             * an error in this case.) */
            while (!canSeek || remaining > 0)
            {
                var bytesRead = await source.ReadAsync(buffer, 0, size);

                if (bytesRead <= 0)
                {
                    if (canSeek)
                        throw new EndOfStreamException($"End of stream reached, but {remaining} bytes remained to be read.");
                    else
                        break;
                }

                await destination.WriteAsync(buffer, 0, bytesRead);
                remaining -= canSeek ? bytesRead : 0;
            }
        }
    }
}
