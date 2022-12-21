﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Ar file extractor.
    /// </summary>
    public class GnuArExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public GnuArExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an archive file created with GNU ar
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            await foreach (var entry in ArFile.GetFileEntriesAsync(fileEntry, options, governor))
            {
                if (options.Recurse || topLevel)
                {
                    await foreach (var extractedFile in Context.ExtractAsync(entry, options, governor, false))
                    {
                        yield return extractedFile;
                    }
                }
                else
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        ///     Extracts an archive file created with GNU ar
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            IEnumerable<FileEntry>? fileEntries = null;
            try
            {
                fileEntries = ArFile.GetFileEntries(fileEntry, options, governor);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.AR, fileEntry.FullPath, string.Empty, e.GetType());
                if (e is OverflowException)
                {
                    throw;
                }
            }
            if (fileEntries != null)
            {
                if (options.Parallel)
                {
                    ConcurrentBag<FileEntry> batch = new();
                    using var enumerator = fileEntries.GetEnumerator();
                    
                    bool hasMore = enumerator.MoveNext();
                    while (hasMore)
                    {
                        batch = new();
                        ConcurrentStack<FileEntry> tempStore = new();
                        for (int i = 0; i < options.BatchSize; i++)
                        {
                            batch.Add(enumerator.Current);
                            hasMore = enumerator.MoveNext();
                            if (!hasMore)
                            {
                                break;
                            }
                        }

                        try
                        {
                            Parallel.ForEach(batch, arEntry =>
                            {
                                if (options.Recurse || topLevel)
                                {
                                    var entries = Context.Extract(arEntry, options, governor, false).ToArray();
                                    if (entries.Any())
                                    {
                                        tempStore.PushRange(entries);
                                    }
                                }
                                else
                                {
                                    tempStore.Push(arEntry);
                                }
                            });
                        }
                        catch (AggregateException e) when (e.InnerException is OverflowException or TimeoutException)
                        {
                            throw e.InnerException;
                        }

                        foreach (var entry in tempStore)
                        {
                            yield return entry;
                        }
                    }
                }
                else
                {
                    foreach (var entry in fileEntries)
                    {
                        if (options.Recurse || topLevel)
                        {
                            foreach (var extractedFile in Context.Extract(entry, options, governor, false))
                            {
                                yield return extractedFile;
                            }
                        }
                        else
                        {
                            yield return entry;
                        }
                    }
                }
            }
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
        }
    }
}