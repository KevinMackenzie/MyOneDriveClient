using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocalCloudStorage
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

        public static async Task<string> ReadAllToStringAsync(this Stream source, Encoding encoding)
        {
            return await (new StreamReader(source, encoding)).ReadToEndAsync();
        }
        //public static async Task<string> ReadAllToStringAsync(this Stream source, Encoding encoding,
        //    CancellationToken ct)
        //{
        //    return await ReadAllToStringInternalAsync(source, encoding, ct);
        //}
        public static async Task<string> ReadAllToStringAsync(this Stream source, Encoding encoding, CancellationToken ct)
        {
            return await (new StreamReader(new CancellableStream(source, ct), encoding)).ReadToEndAsync();
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
            await CopyToStreamInternalAsync(source, destination, null, chunkSize);
        }
        public static async Task CopyToStreamAsync(this Stream source, Stream destination, CancellationToken ct,
            int chunkSize = 4096)
        {
            await CopyToStreamInternalAsync(source, destination, ct, chunkSize);
        }
        private static async Task CopyToStreamInternalAsync(this Stream source, Stream destination, CancellationToken? ct,
            int chunkSize = 4096)
        {
            ct?.ThrowIfCancellationRequested();

            //parameter checks
            if (source == null)
                throw new ArgumentNullException(nameof(source), "Source stream must not be null");
            if (destination == null)
                throw new ArgumentNullException(nameof(destination), "Destination stream must not be null");
            if (!source.CanRead)
                throw new ArgumentException("Source stream must support reading", nameof(source));
            if (!destination.CanWrite)
                throw new ArgumentException("Destination stream must support writing", nameof(destination));

            if (chunkSize < 1)
                throw new ArgumentException("Buffer size for stream copying must be more than 1", nameof(chunkSize));

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
                ct?.ThrowIfCancellationRequested();
                var bytesRead = await source.ReadAsync(buffer, 0, size);

                if (bytesRead <= 0)
                {
                    if (canSeek)
                        throw new EndOfStreamException($"End of stream reached, but {remaining} bytes remained to be read.");
                    else
                        break;
                }

                ct?.ThrowIfCancellationRequested();

                await destination.WriteAsync(buffer, 0, bytesRead);
                remaining -= canSeek ? bytesRead : 0;
            }
        }

        public static async Task DelayNoThrow(TimeSpan delay, CancellationToken ct)
        {
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (TaskCanceledException)
            { }
        }
        public static async Task DelayNoThrow(TimeSpan delay, TimeSpan resolution, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var later = now + delay;
            while (now < later)
            {
                await DelayNoThrow(resolution, ct);
                now = DateTime.UtcNow;
            }
        }
        public static async Task Delay(TimeSpan delay, TimeSpan resolution)
        {
            var now = DateTime.UtcNow;
            var later = now + delay;
            while (now < later)
            {
                await Task.Delay(resolution);
                now = DateTime.UtcNow;
            }
        }
        
        public static void LogException(Exception e)
        {
            Debug.WriteLine($"Exception of type {e.GetType()} when calling \"{e.TargetSite}\" with message \"{e.Message}\"");
            Debug.Indent();
            Debug.WriteLine(e.StackTrace);
            Debug.Unindent();
        }


        public static void DeleteNullElements<T>(this List<T> list) where T : class
        {
            while (list.Remove(null)) ;
        }
    }
}
