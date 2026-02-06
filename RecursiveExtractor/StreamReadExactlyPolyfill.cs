// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

#if !NET7_0_OR_GREATER
using System.IO;

namespace Microsoft.CST.RecursiveExtractor
{
    internal static class StreamReadExactlyPolyfill
    {
        internal static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }
                totalRead += read;
            }
        }
    }
}
#endif
