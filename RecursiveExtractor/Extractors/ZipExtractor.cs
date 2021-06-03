using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Zip Archive Extraction Implementation
    /// </summary>
    public class ZipExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public ZipExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }
        private const int BUFFER_SIZE = 32768;

        private string? GetZipPassword(FileEntry fileEntry, ZipFile zipFile, ZipEntry zipEntry, ExtractorOptions options)
        {
            foreach (var passwords in options.Passwords.Where(x => x.Key.IsMatch(fileEntry.Name)))
            {
                foreach (var password in passwords.Value)
                {
                    zipFile.Password = password;
                    try
                    {
                        using var zipStream = zipFile.GetInputStream(zipEntry);
                        return password;
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                    }
                }
            }
            return null;
        }

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
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
                var buffer = new byte[BUFFER_SIZE];
                var passwordFound = false;
                foreach (ZipEntry? zipEntry in zipFile)
                {
                    if (zipEntry?.IsDirectory != false ||
                        !zipEntry.CanDecompress)
                    {
                        continue;
                    }

                    if (zipEntry.IsCrypted && !passwordFound)
                    {
                        zipFile.Password = GetZipPassword(fileEntry, zipFile, zipEntry, options) ?? string.Empty;
                        passwordFound = true;
                    }

                    governor.CheckResourceGovernor(zipEntry.Size);

                    using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                    try
                    {
                        var zipStream = zipFile.GetInputStream(zipEntry);
                        StreamUtils.Copy(zipStream, fs, buffer);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                    }

                    var name = zipEntry.Name.Replace('/', Path.DirectorySeparatorChar);
                    if (options.FileNamePasses($"{fileEntry.FullPath}{Path.DirectorySeparatorChar}{name}"))
                    {
                        var newFileEntry = new FileEntry(name, fs, fileEntry, modifyTime: zipEntry.DateTime, memoryStreamCutoff: options.MemoryStreamCutoff);

                        if (newFileEntry != null)
                        {
                            if (options.Recurse || topLevel)
                            {
                                await foreach (var innerEntry in Context.ExtractAsync(newFileEntry, options, governor, false))
                                {
                                    yield return innerEntry;
                                }
                            }
                            else
                            {
                                yield return newFileEntry;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
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
                var buffer = new byte[BUFFER_SIZE];
                var passwordFound = false;
                foreach (ZipEntry? zipEntry in zipFile)
                {
                    if (zipEntry?.IsDirectory != false ||
                        !zipEntry.CanDecompress)
                    {
                        continue;
                    }

                    governor.CheckResourceGovernor(zipEntry.Size);

                    using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);

                    if (zipEntry.IsCrypted && !passwordFound)
                    {
                        zipFile.Password = GetZipPassword(fileEntry, zipFile, zipEntry, options) ?? string.Empty;
                        passwordFound = true;
                    }

                    try
                    {
                        var zipStream = zipFile.GetInputStream(zipEntry);
                        StreamUtils.Copy(zipStream, fs, buffer);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                    }

                    var name = zipEntry.Name.Replace('/', Path.DirectorySeparatorChar);
                    if (options.FileNamePasses($"{fileEntry.FullPath}{Path.DirectorySeparatorChar}{name}"))
                    {
                        var newFileEntry = new FileEntry(name, fs, fileEntry, modifyTime: zipEntry.DateTime, memoryStreamCutoff: options.MemoryStreamCutoff);

                        if (options.Recurse || topLevel)
                        {
                            foreach (var innerEntry in Context.Extract(newFileEntry, options, governor, false))
                            {
                                yield return innerEntry;
                            }
                        }
                        else
                        {
                            yield return newFileEntry;
                        }
                    }
                }
            }
        }

        private const int bufferSize = 4096;
    }
}
