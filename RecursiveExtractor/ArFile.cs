// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor
{

    /// <summary>
    ///  Ar file parser.  Supports SystemV style lookup tables in both 32 and 64 bit mode as well as BSD and GNU formatted .ars.
    /// </summary>
    public static class ArFile
    {
        /// <summary>
        /// Get the FileEntries contained in the FileEntry representing an Ar file
        /// </summary>
        /// <param name="fileEntry">The FileEntry to parse</param>
        /// <param name="options">The ExtractorOptions</param>
        /// <param name="governor">The RsourceGovernor to use</param>
        /// <returns>The FileEntries found.</returns>
        public static IEnumerable<FileEntry> GetFileEntries(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            if (fileEntry == null)
            {
                yield break;
            }
            // First, cut out the file signature (8 bytes)
            fileEntry.Content.Position = 8;
            var filenameLookup = new Dictionary<int, string>();
            var headerBuffer = new byte[60];
            while (true)
            {
                if (fileEntry.Content.Length - fileEntry.Content.Position < 60)  // The header for each file is 60 bytes
                {
                    break;
                }

                fileEntry.Content.Read(headerBuffer, 0, 60);
                var headerString = Encoding.ASCII.GetString(headerBuffer);
                if (long.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var size))// header size in bytes
                {
                    governor.CheckResourceGovernor(size);
                    governor.CurrentOperationProcessedBytesLeft -= size;
                    var filename = Encoding.ASCII.GetString(headerBuffer[0..16]).Trim();

                    // Header with list of file names
                    if (filename.StartsWith("//"))
                    {
                        // This should just be a list of names, size should be safe to load in memory and cast
                        // to int
                        var fileNamesBytes = new byte[size];
                        fileEntry.Content.Read(fileNamesBytes, 0, (int)size);

                        var name = new StringBuilder();
                        var index = 0;
                        for (var i = 0; i < fileNamesBytes.Length; i++)
                        {
                            if (fileNamesBytes[i] == '/')
                            {
                                filenameLookup.Add(index, name.ToString());
                                name.Clear();
                            }
                            else if (fileNamesBytes[i] == '\n')
                            {
                                // The next filename would start on the next line
                                index = i + 1;
                            }
                            else
                            {
                                name.Append((char)fileNamesBytes[i]);
                            }
                        }
                    }
                    else if (filename.StartsWith("#1/"))
                    {
                        // We should be positioned right after the header
                        if (int.TryParse(filename.Substring(3), out var nameLength))
                        {
                            var nameSpan = new byte[nameLength];
                            // This should move us right to the file

                            fileEntry.Content.Read(nameSpan, 0, nameLength);

                            var entryStream = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);

                            // The name length is included in the total size reported in the header
                            CopyStreamBytes(fileEntry.Content, entryStream, size - nameLength);

                            yield return new FileEntry(Encoding.ASCII.GetString(nameSpan).TrimEnd('/'), entryStream, fileEntry, true);
                        }
                    }
                    else if (filename.Equals('/'))
                    {
                        // System V symbol lookup table N = 32 bit big endian integers (entries in table) then
                        // N 32 bit big endian integers representing prositions in archive then N \0
                        // terminated strings "symbol name" (possibly filename)

                        var tableContents = new byte[size];
                        fileEntry.Content.Read(tableContents, 0, (int)size);

                        var numEntries = IntFromBigEndianBytes(tableContents[0..4]);
                        var filePositions = new int[numEntries];
                        for (var i = 0; i < numEntries; i++)
                        {
                            var start = (i + 1) * 4;
                            var end = start + 4;
                            filePositions[i] = IntFromBigEndianBytes(tableContents[start..end]);
                        }

                        var index = 0;
                        var sb = new StringBuilder();
                        var fileEntries = new List<(int, string)>();

                        for (var i = 0; i < tableContents.Length; i++)
                        {
                            if (tableContents[i] == '\0')
                            {
                                fileEntries.Add((filePositions[index++], sb.ToString()));
                                sb.Clear();
                            }
                            else
                            {
                                sb.Append(tableContents[i]);
                            }
                        }

                        foreach (var entry in fileEntries)
                        {
                            fileEntry.Content.Position = entry.Item1;
                            fileEntry.Content.Read(headerBuffer, 0, 60);

                            if (long.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var innerSize))// header size in bytes
                            {
                                if (filename.StartsWith("/"))
                                {
                                    if (int.TryParse(filename[1..], out var innerIndex))
                                    {
                                        try
                                        {
                                            filename = filenameLookup[innerIndex];
                                        }
                                        catch (Exception)
                                        {
                                            Logger.Debug("Expected to find a filename at index {0}", innerIndex);
                                        }
                                    }
                                }
                                else
                                {
                                    filename = entry.Item2;
                                }
                                var entryStream = innerSize > options.MemoryStreamCutoff ?
                                    new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                    (Stream)new MemoryStream((int)innerSize);
                                CopyStreamBytes(fileEntry.Content, entryStream, innerSize);
                                yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                            }
                        }
                        fileEntry.Content.Position = fileEntry.Content.Length - 1;
                    }
                    else if (filename.Equals("/SYM64/"))
                    {
                        // https://en.wikipedia.org/wiki/Ar_(Unix)#System_V_(or_GNU)_variant GNU lookup table
                        // (archives larger than 4GB) N = 64 bit big endian integers (entries in table) then N
                        // 64 bit big endian integers representing positions in archive then N \0 terminated
                        // strings "symbol name" (possibly filename)

                        var buffer = new byte[8];
                        fileEntry.Content.Read(buffer, 0, 8);

                        var numEntries = Int64FromBigEndianBytes(buffer);
                        var filePositions = new long[numEntries];

                        for (var i = 0; i < numEntries; i++)
                        {
                            fileEntry.Content.Read(buffer, 0, 8);
                            filePositions[i] = Int64FromBigEndianBytes(buffer);
                        }

                        var index = 0;
                        var sb = new StringBuilder();
                        var fileEntries = new List<(long, string)>();

                        while (fileEntry.Content.Position < size)
                        {
                            fileEntry.Content.Read(buffer, 0, 1);
                            if (buffer[0] == '\0')
                            {
                                fileEntries.Add((filePositions[index++], sb.ToString()));
                                sb.Clear();
                            }
                            else
                            {
                                sb.Append(buffer[0]);
                            }
                        }

                        foreach (var innerEntry in fileEntries)
                        {
                            fileEntry.Content.Position = innerEntry.Item1;

                            fileEntry.Content.Read(headerBuffer, 0, 60);

                            if (long.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var innerSize))// header size in bytes
                            {
                                if (filename.StartsWith("/"))
                                {
                                    if (int.TryParse(filename[1..], out var innerIndex))
                                    {
                                        try
                                        {
                                            filename = filenameLookup[innerIndex];
                                        }
                                        catch (Exception)
                                        {
                                            Logger.Debug("Expected to find a filename at index {0}", innerIndex);
                                        }
                                    }
                                }
                                else
                                {
                                    filename = innerEntry.Item2;
                                }
                                var entryStream = innerSize > options.MemoryStreamCutoff ?
                                    new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                    (Stream)new MemoryStream((int)innerSize);
                                CopyStreamBytes(fileEntry.Content, entryStream, innerSize);

                                yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                            }
                        }
                        fileEntry.Content.Position = fileEntry.Content.Length - 1;
                    }
                    else if (filename.StartsWith("/"))
                    {
                        if (int.TryParse(filename[1..], out var index))
                        {
                            try
                            {
                                filename = filenameLookup[index];
                            }
                            catch (Exception)
                            {
                                Logger.Debug("Expected to find a filename at index {0}", index);
                            }
                        }
                        var entryStream = size > options.MemoryStreamCutoff ?
                                    new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                    (Stream)new MemoryStream((int)size);
                        CopyStreamBytes(fileEntry.Content, entryStream, size);

                        yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                    }
                    else
                    {
                        var entryStream = size > options.MemoryStreamCutoff ?
                                    new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                    (Stream)new MemoryStream((int)size);
                        CopyStreamBytes(fileEntry.Content, entryStream, size);

                        yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                    }
                }
                else
                {
                    // Not a valid header, we couldn't parse the file size.
                    yield break;
                }

                // Entries are padded on even byte boundaries https://docs.oracle.com/cd/E36784_01/html/E36873/ar.h-3head.html
                fileEntry.Content.Position = fileEntry.Content.Position % 2 == 1 ? fileEntry.Content.Position + 1 : fileEntry.Content.Position;
            }
        }

        /// <summary>
        /// Get the FileEntries contained in the FileEntry representing an Ar file
        /// </summary>
        /// <param name="fileEntry">The FileEntry to parse</param>
        /// <param name="options">The ExtractorOptions</param>
        /// <param name="governor">The RsourceGovernor to use</param>
        /// <returns>The FileEntries found.</returns>
        public static async IAsyncEnumerable<FileEntry> GetFileEntriesAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            if (fileEntry == null)
            {
                yield break;
            }
            // First, cut out the file signature (8 bytes)
            fileEntry.Content.Position = 8;
            var filenameLookup = new Dictionary<int, string>();
            var headerBuffer = new byte[60];
            while (true)
            {
                if (fileEntry.Content.Length - fileEntry.Content.Position < 60)  // The header for each file is 60 bytes
                {
                    break;
                }

                fileEntry.Content.Read(headerBuffer, 0, 60);
                var headerString = Encoding.ASCII.GetString(headerBuffer);
                if (long.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var size))// header size in bytes
                {
                    governor.CheckResourceGovernor(size);
                    governor.CurrentOperationProcessedBytesLeft -= size;
                    var filename = Encoding.ASCII.GetString(headerBuffer[0..16]).Trim();

                    // Header with list of file names
                    if (filename.StartsWith("//"))
                    {
                        // This should just be a list of names, size should be safe to load in memory and cast
                        // to int
                        var fileNamesBytes = new byte[size];
                        await fileEntry.Content.ReadAsync(fileNamesBytes, 0, (int)size);

                        var name = new StringBuilder();
                        var index = 0;
                        for (var i = 0; i < fileNamesBytes.Length; i++)
                        {
                            if (fileNamesBytes[i] == '/')
                            {
                                filenameLookup.Add(index, name.ToString());
                                name.Clear();
                            }
                            else if (fileNamesBytes[i] == '\n')
                            {
                                // The next filename would start on the next line
                                index = i + 1;
                            }
                            else
                            {
                                name.Append((char)fileNamesBytes[i]);
                            }
                        }
                    }
                    else if (filename.StartsWith("#1/"))
                    {
                        // We should be positioned right after the header
                        if (int.TryParse(filename.Substring(3), out var nameLength))
                        {
                            var nameSpan = new byte[nameLength];
                            // This should move us right to the file
                            await fileEntry.Content.ReadAsync(nameSpan, 0, nameLength);

                            var entryStream = size - nameLength > options.MemoryStreamCutoff ?
                                                                new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                                                (Stream)new MemoryStream((int)size - nameLength);
                            // The name length is included in the total size reported in the header
                            await CopyStreamBytesAsync(fileEntry.Content, entryStream, size - nameLength);

                            yield return new FileEntry(Encoding.ASCII.GetString(nameSpan).TrimEnd('/'), entryStream, fileEntry, true);
                        }
                    }
                    else if (filename.Equals('/'))
                    {
                        // System V symbol lookup table N = 32 bit big endian integers (entries in table) then
                        // N 32 bit big endian integers representing prositions in archive then N \0
                        // terminated strings "symbol name" (possibly filename)

                        var tableContents = new byte[size];
                        fileEntry.Content.Read(tableContents, 0, (int)size);

                        var numEntries = IntFromBigEndianBytes(tableContents[0..4]);
                        var filePositions = new int[numEntries];
                        for (var i = 0; i < numEntries; i++)
                        {
                            var start = (i + 1) * 4;
                            var end = start + 4;
                            filePositions[i] = IntFromBigEndianBytes(tableContents[start..end]);
                        }

                        var index = 0;
                        var sb = new StringBuilder();
                        var fileEntries = new List<(int, string)>();

                        for (var i = 0; i < tableContents.Length; i++)
                        {
                            if (tableContents[i] == '\0')
                            {
                                fileEntries.Add((filePositions[index++], sb.ToString()));
                                sb.Clear();
                            }
                            else
                            {
                                sb.Append(tableContents[i]);
                            }
                        }

                        foreach (var entry in fileEntries)
                        {
                            fileEntry.Content.Position = entry.Item1;
                            await fileEntry.Content.ReadAsync(headerBuffer, 0, 60);

                            if (long.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var innerSize))// header size in bytes
                            {
                                if (filename.StartsWith("/"))
                                {
                                    if (int.TryParse(filename[1..], out var innerIndex))
                                    {
                                        try
                                        {
                                            filename = filenameLookup[innerIndex];
                                        }
                                        catch (Exception)
                                        {
                                            Logger.Debug("Expected to find a filename at index {0}", innerIndex);
                                        }
                                    }
                                }
                                else
                                {
                                    filename = entry.Item2;
                                }
                                var entryStream = innerSize > options.MemoryStreamCutoff ?
                                    new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                    (Stream)new MemoryStream((int)innerSize);
                                await CopyStreamBytesAsync(fileEntry.Content, entryStream, innerSize);
                                yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                            }
                        }
                        fileEntry.Content.Position = fileEntry.Content.Length - 1;
                    }
                    else if (filename.Equals("/SYM64/"))
                    {
                        // https://en.wikipedia.org/wiki/Ar_(Unix)#System_V_(or_GNU)_variant GNU lookup table
                        // (archives larger than 4GB) N = 64 bit big endian integers (entries in table) then N
                        // 64 bit big endian integers representing positions in archive then N \0 terminated
                        // strings "symbol name" (possibly filename)

                        var buffer = new byte[8];
                        fileEntry.Content.Read(buffer, 0, 8);

                        var numEntries = Int64FromBigEndianBytes(buffer);
                        var filePositions = new long[numEntries];

                        for (var i = 0; i < numEntries; i++)
                        {
                            fileEntry.Content.Read(buffer, 0, 8);
                            filePositions[i] = Int64FromBigEndianBytes(buffer);
                        }

                        var index = 0;
                        var sb = new StringBuilder();
                        var fileEntries = new List<(long, string)>();

                        while (fileEntry.Content.Position < size)
                        {
                            fileEntry.Content.Read(buffer, 0, 1);
                            if (buffer[0] == '\0')
                            {
                                fileEntries.Add((filePositions[index++], sb.ToString()));
                                sb.Clear();
                            }
                            else
                            {
                                sb.Append(buffer[0]);
                            }
                        }

                        foreach (var innerEntry in fileEntries)
                        {
                            fileEntry.Content.Position = innerEntry.Item1;

                            fileEntry.Content.Read(headerBuffer, 0, 60);

                            if (long.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var innerSize))// header size in bytes
                            {
                                if (filename.StartsWith("/"))
                                {
                                    if (int.TryParse(filename[1..], out var innerIndex))
                                    {
                                        try
                                        {
                                            filename = filenameLookup[innerIndex];
                                        }
                                        catch (Exception)
                                        {
                                            Logger.Debug("Expected to find a filename at index {0}", innerIndex);
                                        }
                                    }
                                }
                                else
                                {
                                    filename = innerEntry.Item2;
                                }

                                var entryStream = innerSize > options.MemoryStreamCutoff ?
                                    new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                                    (Stream)new MemoryStream((int)innerSize);
                                await CopyStreamBytesAsync(fileEntry.Content, entryStream, innerSize);
                                yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                            }
                        }
                        fileEntry.Content.Position = fileEntry.Content.Length - 1;
                    }
                    else if (filename.StartsWith("/"))
                    {
                        if (int.TryParse(filename[1..], out var index))
                        {
                            try
                            {
                                filename = filenameLookup[index];
                            }
                            catch (Exception)
                            {
                                Logger.Debug("Expected to find a filename at index {0}", index);
                            }
                        }
                        var entryStream = size > options.MemoryStreamCutoff ?
                            new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                            (Stream)new MemoryStream((int)size);
                        CopyStreamBytes(fileEntry.Content, entryStream, size);
                        yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);
                    }
                    else
                    {
                        var entryStream = size > options.MemoryStreamCutoff ?
                            new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose) :
                            (Stream)new MemoryStream((int)size);
                        await CopyStreamBytesAsync(fileEntry.Content, entryStream, size);
                        yield return new FileEntry(filename.TrimEnd('/'), entryStream, fileEntry, true);

                    }
                }
                else
                {
                    // Not a valid header, we couldn't parse the file size.
                    yield break;
                }

                // Entries are padded on even byte boundaries https://docs.oracle.com/cd/E36784_01/html/E36873/ar.h-3head.html
                fileEntry.Content.Position = fileEntry.Content.Position % 2 == 1 ? fileEntry.Content.Position + 1 : fileEntry.Content.Position;
            }
        }

        public static long Int64FromBigEndianBytes(byte[] value)
        {
            if (value.Length == 8)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }
                return BitConverter.ToInt64(value, 0);
            }
            return -1;
        }

        public static int IntFromBigEndianBytes(byte[] value)
        {
            if (value.Length == 4)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(value);
                }
                return BitConverter.ToInt32(value, 0);
            }
            return -1;
        }

        internal static void CopyStreamBytes(Stream input, Stream output, long bytes)
        {
            var buffer = new byte[32768];
            long read;
            while (bytes > 0 &&
                   (read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, bytes))) > 0)
            {
                output.Write(buffer, 0, (int)read);
                bytes -= read;
            }
        }

        internal static async Task<long> CopyStreamBytesAsync(Stream input, Stream output, long bytes)
        {
            var buffer = new byte[32768];
            long read;
            long totalRead = 0;
            while (bytes > 0 &&
                   (read = await input.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, bytes))) > 0)
            {
                totalRead += read;
                await output.WriteAsync(buffer, 0, (int)read);
                bytes -= read;
            }
            return totalRead;
        }

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}