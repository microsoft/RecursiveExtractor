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
            var needsPassword = false;
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e) when (e is SharpCompress.Common.CryptographicException)
            {
                needsPassword = true;
            }
            catch (Exception e)
            {                
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
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
        ///<inheritdoc />
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
                    using var enumerator = entries.GetEnumerator();
                    ConcurrentBag<SevenZipArchiveEntry> entryBatch = new();
                    bool moreAvailable = enumerator.MoveNext();
                    while (moreAvailable)
                    {
                        entryBatch = new();
                        for (int i = 0; i < options.BatchSize; i++)
                        {
                            entryBatch.Add(enumerator.Current);
                            moreAvailable = enumerator.MoveNext();
                            if (!moreAvailable)
                            {
                                break;
                            }
                        }

                        if (entryBatch.Count == 0)
                        {
                            break;
                        }
                        var batchSize = Math.Min(options.BatchSize, entries.Count);
                        var selectedEntries = entries.GetRange(0, batchSize).Select(entry => (entry, entry.OpenEntryStream())).ToArray();
                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.entry.Size));

                        try
                        {
                            Parallel.ForEach(selectedEntries,entry =>
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
                        catch (AggregateException e) when (e.InnerException is OverflowException or TimeoutException)
                        {
                            throw e.InnerException;
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