using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Deb Archive extractor implementation
    /// </summary>
    public class DebExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public DebExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts a .deb file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            await foreach (var entry in DebArchiveFile.GetFileEntriesAsync(fileEntry, options, governor))
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
        ///     Extracts a .deb file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            var failed = false;
            IEnumerable<FileEntry>? entries = null;
            try
            {
                entries = DebArchiveFile.GetFileEntries(fileEntry, options, governor);
            }
            catch (Exception e) when (e is not OverflowException)
            {
                fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.DEB, fileEntry.FullPath, string.Empty, e.GetType());
                if (!options.ExtractSelfOnFail)
                {
                    yield break;
                }
                else
                {
                    failed = true;
                }
            }
            if (failed)
            {
                fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                yield return fileEntry;
                yield break;
            }
            if (entries != null)
            {
                foreach (var entry in entries)
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