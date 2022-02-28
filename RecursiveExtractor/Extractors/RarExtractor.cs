using SharpCompress.Archives.Rar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The RAR Archive extractor implementation
    /// </summary>
    public class RarExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public RarExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        private (RarArchive? archive, FileEntryStatus archiveStatus) GetRarArchive(FileEntry fileEntry, ExtractorOptions options)
        {
            RarArchive? rarArchive = null;
            var needsPassword = false;

            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
                // Test for invalid archives. This will throw invalidformatexception
                var t = rarArchive.IsSolid;
            }
            catch (SharpCompress.Common.CryptographicException)
            {
                needsPassword = true;
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, fileEntry.ArchiveType, fileEntry.FullPath, string.Empty, e.GetType());
                return (null, FileEntryStatus.FailedArchive);
            }

            try
            {
                if (rarArchive?.Entries.Any(x => x.IsEncrypted) ?? false && fileEntry.ArchiveType == ArchiveFileType.RAR5)
                {
                    return (null, FileEntryStatus.EncryptedArchive);
                }
            }
            catch (Exception e) when (e is SharpCompress.Common.CryptographicException || e is SharpCompress.Common.InvalidFormatException)
            {
                needsPassword = true;
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
                            fileEntry.Content.Position = 0;
                            rarArchive = RarArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions() { Password = password, LookForHeader = true });
                            var count = 0; //To do something in the loop
                            foreach (var entry in rarArchive.Entries)
                            {
                                //Just do anything in the loop, but you need to loop over entries to check if the password is correct
                                count++;
                            }
                            passwordFound = true;
                            break;
                        }
                        catch (Exception e)
                        {
                            Logger.Trace(Extractor.FAILED_PASSWORD_STRING, fileEntry.FullPath, ArchiveFileType.RAR, e.GetType(), e.Message);
                        }
                    }
                }
                if (!passwordFound)
                {
                    return (null, FileEntryStatus.EncryptedArchive);
                }
            }
            return (rarArchive, FileEntryStatus.Default);
        }

        /// <summary>
        ///     Extracts a RAR archive
        /// </summary>
        /// <param name="fileEntry">The <see cref="FileEntry"/> to extract.</param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var rarArchive, var archiveType) = GetRarArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveType;
            if (rarArchive != null && fileEntry.EntryStatus == FileEntryStatus.Default)
            {
                foreach (var entry in rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory))
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

        /// <summary>
        ///     Extracts a RAR archive
        /// </summary>
        /// <param name="fileEntry">The <see cref="FileEntry"/> to extract.</param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var rarArchive, var archiveType) = GetRarArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveType;
            if (rarArchive != null && fileEntry.EntryStatus == FileEntryStatus.Default)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory);
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Any())
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count());

                        var streams = entries.Take(batchSize).Select(entry => (entry, entry.OpenEntryStream())).ToList();

                        governor.CheckResourceGovernor(streams.Sum(x => x.Item2.Length));

                        streams.AsParallel().ForAll(streampair =>
                        {
                            try
                            {
                                FileEntry newFileEntry = new FileEntry(streampair.entry.Key, streampair.Item2, fileEntry, false, streampair.entry.CreatedTime, streampair.entry.LastModifiedTime, streampair.entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);

                                if (newFileEntry != null)
                                {
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
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, streampair.entry.Key, e.GetType());
                            }
                        });
                        governor.CheckResourceGovernor(0);

                        entries = entries.Skip(streams.Count);

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
                        FileEntry? newFileEntry = null;
                        try
                        {
                            var stream = entry.OpenEntryStream();
                            var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                            newFileEntry = new FileEntry(name, stream, fileEntry, false, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                        }
                        if (newFileEntry != null)
                        {
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