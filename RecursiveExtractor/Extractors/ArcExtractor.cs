using SharpCompress.Readers;
using SharpCompress.Readers.Arc;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The ARC Archive extractor implementation
    /// </summary>
    public class ArcExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public ArcExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an ARC archive
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            ArcReader? arcReader = null;
            try
            {
                fileEntry.Content.Position = 0;
                arcReader = ArcReader.Open(fileEntry.Content, new ReaderOptions()
                {
                    LeaveStreamOpen = true
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ARC, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (arcReader != null)
            {
                using (arcReader)
                {
                    while (arcReader.MoveToNextEntry())
                    {
                        var entry = arcReader.Entry;
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        var name = entry.Key?.Replace('/', Path.DirectorySeparatorChar);
                        if (string.IsNullOrEmpty(name))
                        {
                            Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.ARC, fileEntry.FullPath);
                            continue;
                        }

                        using (var entryStream = arcReader.OpenEntryStream())
                        {
                            var newFileEntry = await FileEntry.FromStreamAsync(name, entryStream, fileEntry, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
                            if (newFileEntry != null)
                            {
                                // SharpCompress ARC does not expose entry sizes, so we check the resource governor
                                // after extraction using the actual decompressed content length.
                                governor.CheckResourceGovernor(newFileEntry.Content.Length);

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
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
        }

        /// <summary>
        ///     Extracts an ARC archive
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            ArcReader? arcReader = null;
            try
            {
                fileEntry.Content.Position = 0;
                arcReader = ArcReader.Open(fileEntry.Content, new ReaderOptions()
                {
                    LeaveStreamOpen = true
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ARC, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (arcReader != null)
            {
                using (arcReader)
                {
                    while (arcReader.MoveToNextEntry())
                    {
                        var entry = arcReader.Entry;
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        FileEntry? newFileEntry = null;
                        try
                        {
                            using (var stream = arcReader.OpenEntryStream())
                            {
                                var name = entry.Key?.Replace('/', Path.DirectorySeparatorChar);
                                if (string.IsNullOrEmpty(name))
                                {
                                    Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.ARC, fileEntry.FullPath);
                                    continue;
                                }
                                newFileEntry = new FileEntry(name, stream, fileEntry, false, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ARC, fileEntry.FullPath, entry.Key, e.GetType());
                        }
                        if (newFileEntry != null)
                        {
                            // SharpCompress ARC does not expose entry sizes, so we check the resource governor
                            // after extraction using the actual decompressed content length.
                            governor.CheckResourceGovernor(newFileEntry.Content.Length);

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
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
        }
    }
}
