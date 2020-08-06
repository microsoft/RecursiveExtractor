using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class ZipExtractor : AsyncExtractorInterface
    {
        public ZipExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }
        private const int BUFFER_SIZE = 32768;

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            ZipFile? zipFile = null;
            try
            {
                zipFile = new ZipFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipFile != null)
            {
                foreach (ZipEntry? zipEntry in zipFile)
                {
                    if (zipEntry is null ||
                        zipEntry.IsDirectory ||
                        zipEntry.IsCrypted ||
                        !zipEntry.CanDecompress)
                    {
                        continue;
                    }

                    governor.CheckResourceGovernor(zipEntry.Size);

                    using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                    try
                    {
                        var buffer = new byte[BUFFER_SIZE];
                        var zipStream = zipFile.GetInputStream(zipEntry);
                        StreamUtils.Copy(zipStream, fs, buffer);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                    }

                    var newFileEntry = new FileEntry(zipEntry.Name, fs, fileEntry);

                    if (Extractor.IsQuine(newFileEntry))
                    {
                        Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                        throw new OverflowException();
                    }

                    await foreach (var extractedFile in Context.ExtractFileAsync(newFileEntry, options, governor))
                    {
                        yield return extractedFile;
                    }
                }
            }
        }

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            ZipFile? zipFile = null;
            try
            {
                zipFile = new ZipFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipFile != null)
            {
                foreach (ZipEntry? zipEntry in zipFile)
                {
                    if (zipEntry is null ||
                        zipEntry.IsDirectory ||
                        zipEntry.IsCrypted ||
                        !zipEntry.CanDecompress)
                    {
                        continue;
                    }

                    governor.CheckResourceGovernor(zipEntry.Size);

                    using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                    try
                    {
                        var buffer = new byte[BUFFER_SIZE];
                        var zipStream = zipFile.GetInputStream(zipEntry);
                        StreamUtils.Copy(zipStream, fs, buffer);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                    }

                    var newFileEntry = new FileEntry(zipEntry.Name, fs, fileEntry);

                    if (Extractor.IsQuine(newFileEntry))
                    {
                        Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                        throw new OverflowException();
                    }

                    foreach (var extractedFile in Context.ExtractFile(newFileEntry, options, governor))
                    {
                        yield return extractedFile;
                    }
                }
            }
        }

    }
}
