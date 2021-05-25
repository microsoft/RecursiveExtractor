using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CST.RecursiveExtractor
{

    /// <summary>
    /// Implementation of the Deb Archive format
    /// See: https://en.wikipedia.org/wiki/Deb_(file_format)#/media/File:Deb_File_Structure.svg
    /// </summary>
    public static class DebArchiveFile
    {
        /// <summary>
        /// Enumerate the FileEntries in the given Deb file
        /// </summary>
        /// <param name="fileEntry">The Deb file FileEntry</param>
        /// <param name="options">The ExtractorOptions to use</param>
        /// <param name="governor">The ResourceGovernor to use</param>
        /// <returns>The FileEntries found</returns>
        public static IEnumerable<FileEntry> GetFileEntries(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            if (fileEntry == null)
            {
                yield break;
            }

            // First, cut out the file signature (8 bytes) and global header (64 bytes)
            fileEntry.Content.Position = 72;
            var headerBytes = new byte[60];

            while (true)
            {
                if (fileEntry.Content.Length - fileEntry.Content.Position < 60)  // The header for each file is 60 bytes
                {
                    break;
                }
                fileEntry.Content.Read(headerBytes, 0, 60);
                var filename = Encoding.ASCII.GetString(headerBytes[0..16]).Trim();  // filename is 16 bytes
                var fileSizeBytes = headerBytes[48..58]; // File size is decimal-encoded, 10 bytes long
                if (int.TryParse(Encoding.ASCII.GetString(fileSizeBytes).Trim(), out var fileSize))
                {
                    governor.CheckResourceGovernor(fileSize);
                    governor.CurrentOperationProcessedBytesLeft -= fileSize;

                    var entryContent = new byte[fileSize];
                    fileEntry.Content.Read(entryContent, 0, fileSize);
                    var stream = new MemoryStream(entryContent);
                    yield return new FileEntry(filename, stream, fileEntry, true);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Enumerate the FileEntries in the given Deb file asynchronously
        /// </summary>
        /// <param name="fileEntry">The Deb file FileEntry</param>
        /// <param name="options">The ExtractorOptions to use</param>
        /// <param name="governor">The ResourceGovernor to use</param>
        /// <returns>The FileEntries found</returns>
        public static async IAsyncEnumerable<FileEntry> GetFileEntriesAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            if (fileEntry == null)
            {
                yield break;
            }

            // First, cut out the file signature (8 bytes) and global header (64 bytes)
            fileEntry.Content.Position = 72;
            var headerBytes = new byte[60];

            while (true)
            {
                if (fileEntry.Content.Length - fileEntry.Content.Position < 60)  // The header for each file is 60 bytes
                {
                    break;
                }
                fileEntry.Content.Read(headerBytes, 0, 60);
                var filename = Encoding.ASCII.GetString(headerBytes[0..16]).Trim();  // filename is 16 bytes
                var fileSizeBytes = headerBytes[48..58]; // File size is decimal-encoded, 10 bytes long
                if (int.TryParse(Encoding.ASCII.GetString(fileSizeBytes).Trim(), out var fileSize))
                {
                    governor.CheckResourceGovernor(fileSize);
                    governor.CurrentOperationProcessedBytesLeft -= fileSize;

                    var entryContent = new byte[fileSize];
                    await fileEntry.Content.ReadAsync(entryContent, 0, fileSize);
                    var stream = new MemoryStream(entryContent);
                    var entry = new FileEntry(filename, stream, fileEntry, true);
                    if (options.FileNamePasses(entry.FullPath))
                    {
                        yield return entry;
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }
}