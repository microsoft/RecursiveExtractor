using NLog;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The 7Zip extractor implementation
    /// </summary>
    public class SevenZipExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public SevenZipExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        ///<inheritdoc />
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var sevenZipArchive, var archiveStatus) = GetSevenZipArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveStatus;
            if (sevenZipArchive != null && archiveStatus == FileEntryStatus.Default)
            {
                foreach (var entry in sevenZipArchive.Entries.Where(x => !x.IsDirectory && x.IsComplete).ToList())
                {
                    governor.CheckResourceGovernor(entry.Size);
                    var name = (entry.Key ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                    var newFileEntry = await FileEntry.FromStreamAsync(name, entry.OpenEntryStream(), fileEntry, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);

                    if (newFileEntry != null)
                    {
                        try
                        {
                            if (entry.Attrib.HasValue)
                            {
                                newFileEntry.Metadata = new FileEntryMetadata { Mode = entry.Attrib.Value };
                            }
                        }
                        catch (Exception e) { Logger.Trace("Failed to read file attributes: {0}", e.Message); }
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
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    yield return fileEntry;
                }
            }
        }

        private (SevenZipArchive? archive, FileEntryStatus archiveStatus) GetSevenZipArchive(FileEntry fileEntry, ExtractorOptions options)
        {
            SevenZipArchive? sevenZipArchive = null;
            var needsPassword = false;
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
                if (sevenZipArchive.Entries.Where(x => !x.IsDirectory).Any(x => x.IsEncrypted))
                {
                    needsPassword = true;
                }
            }
            catch (Exception e) when (e is SharpCompress.Common.CryptographicException)
            {
                needsPassword = true;
            }
            // Unencrypted archives throw null reference exception on the .IsEncrypted property
            catch (NullReferenceException)
            {
                return (sevenZipArchive, FileEntryStatus.Default);
            }
            catch (Exception e)
            {                
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
                return (sevenZipArchive, FileEntryStatus.FailedArchive);
            }
            if (needsPassword)
            {
                var passwordFound = false;
                foreach (var passwords in options.Passwords.Where(x => x.Key.IsMatch(fileEntry.Name)))
                {
                    if (passwordFound) { break; }
                    foreach (var password in passwords.Value)
                    {
                        try
                        {
                            sevenZipArchive = SevenZipArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions() { Password = password });
                            // When filenames are encrypted we can't access the size of individual files
                            // But if we can accesss the total uncompressed size we have the right password
                            try
                            {
                                var entry = sevenZipArchive.Entries.Where(x => !x.IsDirectory).FirstOrDefault(x => x.IsEncrypted && x.Size > 0);
                                // When filenames are plain, we can access the properties, so the previous statement won't throw
                                // Instead we need to check that we can actually read from the stream
                                using var entryStream = entry?.OpenEntryStream();
                                var bytes = new byte[1];
                                if ((entryStream?.Read(bytes, 0, 1) ?? 0) == 0)
                                {
                                    Logger.Trace(Extractor.FAILED_PASSWORD_ERROR_MESSAGE_STRING, fileEntry.FullPath, ArchiveFileType.P7ZIP);
                                    continue;
                                }
                                return (sevenZipArchive, FileEntryStatus.Default);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            
                        }
                        catch (Exception e)
                        {
                            Logger.Trace(Extractor.FAILED_PASSWORD_ERROR_MESSAGE_STRING, fileEntry.FullPath, ArchiveFileType.P7ZIP, e.GetType(), e.Message);
                        }
                    }
                }
                return (null, FileEntryStatus.EncryptedArchive);
            }
            return (sevenZipArchive, FileEntryStatus.Default);
        }

        /// <summary>
        ///     Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        ///<inheritdoc />
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var sevenZipArchive, var archiveStatus) = GetSevenZipArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveStatus;
            if (sevenZipArchive != null && archiveStatus == FileEntryStatus.Default)
            {
                var entries = sevenZipArchive.Entries.Where(x => !x.IsDirectory && x.IsComplete).ToList();
                foreach (var entry in entries)
                {
                    governor.CheckResourceGovernor(entry.Size);
                    var name = (entry.Key ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                    var newFileEntry = new FileEntry(name, entry.OpenEntryStream(), fileEntry, createTime: entry.CreatedTime, modifyTime: entry.LastModifiedTime, accessTime: entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);

                    try
                    {
                        if (entry.Attrib.HasValue)
                        {
                            newFileEntry.Metadata = new FileEntryMetadata { Mode = entry.Attrib.Value };
                        }
                    }
                    catch (Exception e) { Logger.Trace("Failed to read file attributes: {0}", e.Message); }

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
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    yield return fileEntry;
                }
            }
        }
    }
}