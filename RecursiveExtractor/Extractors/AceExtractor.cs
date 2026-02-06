using SharpCompress.Readers;
using SharpCompress.Readers.Ace;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The ACE Archive extractor implementation
    /// </summary>
    public class AceExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public AceExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an ACE archive
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            AceReader? aceReader = null;
            try
            {
                aceReader = AceReader.Open(fileEntry.Content, new ReaderOptions()
                {
                    LeaveStreamOpen = true
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ACE, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (aceReader != null)
            {
                using (aceReader)
                {
                    while (aceReader.MoveToNextEntry())
                    {
                        var entry = aceReader.Entry;
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        var name = entry.Key?.Replace('/', Path.DirectorySeparatorChar);
                        if (string.IsNullOrEmpty(name))
                        {
                            Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.ACE, fileEntry.FullPath);
                            continue;
                        }

                        governor.CheckResourceGovernor(entry.Size);
                        var newFileEntry = await FileEntry.FromStreamAsync(name, aceReader.OpenEntryStream(), fileEntry, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
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
        ///     Extracts an ACE archive
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            AceReader? aceReader = null;
            try
            {
                aceReader = AceReader.Open(fileEntry.Content, new ReaderOptions()
                {
                    LeaveStreamOpen = true
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ACE, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (aceReader != null)
            {
                using (aceReader)
                {
                    while (aceReader.MoveToNextEntry())
                    {
                        var entry = aceReader.Entry;
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        FileEntry? newFileEntry = null;
                        try
                        {
                            governor.CheckResourceGovernor(entry.Size);
                            var stream = aceReader.OpenEntryStream();
                            var name = entry.Key?.Replace('/', Path.DirectorySeparatorChar);
                            if (string.IsNullOrEmpty(name))
                            {
                                Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.ACE, fileEntry.FullPath);
                                continue;
                            }
                            newFileEntry = new FileEntry(name, stream, fileEntry, false, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ACE, fileEntry.FullPath, entry.Key, e.GetType());
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
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
        }
    }
}
