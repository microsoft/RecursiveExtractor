// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    internal static class StreamCopyExtensions
    {
        /// <summary>
        /// Copies a stream and reports how many bytes moved, without materializing them and
        /// without allocating a fresh buffer per call.
        /// </summary>
        /// <param name="source">Stream to read from.</param>
        /// <param name="destination">Stream to write to.</param>
        /// <param name="buffer">Scratch buffer reused across calls.</param>
        /// <returns>The number of bytes copied.</returns>
        internal static long CopyToCounting(this Stream source, Stream destination, byte[] buffer)
        {
            long total = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, read);
                total += read;
            }

            return total;
        }
    }
}
