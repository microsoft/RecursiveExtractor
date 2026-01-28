using SharpCompress.Readers.Arj;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The ARJ Archive extractor implementation
    /// </summary>
    public class ArjExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public ArjExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an ARJ archive
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            ArjReader? arjReader = null;
            try
            {
                arjReader = ArjReader.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions()
                {
                    LeaveStreamOpen = true
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ARJ, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (arjReader != null)
            {
                using (arjReader)
                {
                    while (arjReader.MoveToNextEntry())
                    {
                        var entry = arjReader.Entry;
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        governor.CheckResourceGovernor(entry.Size);
                        var name = entry.Key?.Replace('/', Path.DirectorySeparatorChar);
                        if (string.IsNullOrEmpty(name))
                        {
                            Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.ARJ, fileEntry.FullPath);
                            continue;
                        }

                        var newFileEntry = await FileEntry.FromStreamAsync(name, arjReader.OpenEntryStream(), fileEntry, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
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
        ///     Extracts an ARJ archive
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            ArjReader? arjReader = null;
            try
            {
                arjReader = ArjReader.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions()
                {
                    LeaveStreamOpen = true
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ARJ, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (arjReader != null)
            {
                using (arjReader)
                {
                    while (arjReader.MoveToNextEntry())
                    {
                        var entry = arjReader.Entry;
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        governor.CheckResourceGovernor(entry.Size);
                        FileEntry? newFileEntry = null;
                        try
                        {
                            var stream = arjReader.OpenEntryStream();
                            var name = entry.Key?.Replace('/', Path.DirectorySeparatorChar);
                            if (string.IsNullOrEmpty(name))
                            {
                                Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.ARJ, fileEntry.FullPath);
                                continue;
                            }
                            newFileEntry = new FileEntry(name, stream, fileEntry, false, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ARJ, fileEntry.FullPath, entry.Key, e.GetType());
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
