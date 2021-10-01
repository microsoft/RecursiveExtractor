using NLog;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">ExtractorOptions for performing the extraction</param>
        /// <param name="governor">The ResourceGovernor to use.</param>
        /// <param name="topLevel">Is this the top level archive.</param>
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
                    var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                    var newFileEntry = await FileEntry.FromStreamAsync(name, entry.OpenEntryStream(), fileEntry, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);

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
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {                
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
                return (sevenZipArchive, FileEntryStatus.FailedArchive);
            }
            var needsPassword = false;
            try
            {
                // This is a workaround because SharpCompress does not expose if a 7z archive is encrypted without trying to decrypt it
                needsPassword = sevenZipArchive?.TotalUncompressSize == 0;
            }
            // SharpCompress throws an ArgumentNullException from AESDecoderStream.ctor when an archive is encrypted but the password is null
            catch (ArgumentNullException)
            {
                needsPassword = true;
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
                return (sevenZipArchive, FileEntryStatus.FailedArchive);
            }
            if (needsPassword is true)
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
                            if (sevenZipArchive.TotalUncompressSize > 0)
                            {
                                passwordFound = true;
                                return (sevenZipArchive, FileEntryStatus.Default);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Trace(Extractor.FAILED_PASSWORD_STRING, fileEntry.FullPath, ArchiveFileType.P7ZIP, e.GetType(), e.Message);
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
        /// <param name="fileEntry">FileEntry to extract </param>
        /// <param name="options">ExtractorOptions for performing the extraction</param>
        /// <param name="governor">The ResourceGovernor to use.</param>
        /// <param name="topLevel">Is this the top level archive.</param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var sevenZipArchive, var archiveStatus) = GetSevenZipArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveStatus;
            if (sevenZipArchive != null && archiveStatus == FileEntryStatus.Default)
            {
                var entries = sevenZipArchive.Entries.Where(x => !x.IsDirectory && x.IsComplete).ToList();
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Count > 0)
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count);
                        var selectedEntries = entries.GetRange(0, batchSize).Select(entry => (entry, entry.OpenEntryStream()));
                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.entry.Size));

                        try
                        {
                            selectedEntries.AsParallel().ForAll(entry =>
                            {
                                try
                                {
                                    var name = entry.entry.Key.Replace('/', Path.DirectorySeparatorChar);
                                    var newFileEntry = new FileEntry(name, entry.Item2, fileEntry, false, entry.entry.CreatedTime, entry.entry.LastModifiedTime, entry.entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                                    if (options.Recurse || topLevel)
                                    {
                                        var entries = Context.Extract(newFileEntry, options, governor, false);
                                        if (entries.Any())
                                        {
                                            files.PushRange(entries.ToArray());
                                        }
                                    }
                                    else
                                    {
                                        files.Push(newFileEntry);
                                    }
                                }
                                catch (Exception e) when (e is OverflowException)
                                {
                                    Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.entry.Key, e.GetType());
                                    throw;
                                }
                                catch (Exception e)
                                {
                                    Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.entry.Key, e.GetType());
                                }
                            });
                        }
                        catch (Exception e) when (e is AggregateException)
                        {
                            if (e.InnerException?.GetType() == typeof(OverflowException))
                            {
                                throw e.InnerException;
                            }
                            throw;
                        }

                        governor.CheckResourceGovernor(0);
                        entries.RemoveRange(0, batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        governor.CheckResourceGovernor(entry.Size);
                        var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                        var newFileEntry = new FileEntry(name, entry.OpenEntryStream(), fileEntry, createTime: entry.CreatedTime, modifyTime: entry.LastModifiedTime, accessTime: entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);

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