using ICSharpCode.SharpZipLib.GZip;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Gzip extractor implementation
    /// </summary>
    public class GzipExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public GzipExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        // Uncompressed Size not exposed, so estimate a compression ratio of up to 20x for selecting between memory and file stream
        const int CompressionRatioEstimate = 20;

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an Gzip file contained in fileEntry. Since this function is recursive, even though
        ///     Gzip only supports a single compressed file, that inner file could itself contain multiple others.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            using var fs = StreamFactory.GenerateAppropriateBackingStream(options, fileEntry.Content.Length * CompressionRatioEstimate);
            var failed = false;
            try
            {
                GZip.Decompress(fileEntry.Content, fs, false);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, "GZip", e.GetType(), e.Message, e.StackTrace);
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
            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            if (fileEntry.Name.EndsWith(".tgz", StringComparison.InvariantCultureIgnoreCase))
            {
                newFilename = newFilename[0..^4] + ".tar";
            }

            var entry = await FileEntry.FromStreamAsync(newFilename, fs, fileEntry).ConfigureAwait(false);

            if (entry != null)
            {
                if (options.Recurse || topLevel)
                {
                    await foreach (var newFileEntry in Context.ExtractAsync(entry, options, governor, false))
                    {
                        yield return newFileEntry;
                    }
                }
                else
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        ///     Extracts a Gzip file contained in fileEntry. Since this function is recursive, even though
        ///     Gzip only supports a single compressed file, that inner file could itself contain multiple others.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            using var fs = StreamFactory.GenerateAppropriateBackingStream(options, fileEntry.Content.Length * CompressionRatioEstimate);
            var failed = false;
            try
            {
                GZip.Decompress(fileEntry.Content, fs, false);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, "GZip", e.GetType(), e.Message, e.StackTrace);
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

            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
            if (fileEntry.Name.EndsWith(".tgz", StringComparison.InvariantCultureIgnoreCase))
            {
                newFilename = newFilename[0..^4] + ".tar";
            }

            var entry = new FileEntry(newFilename, fs, fileEntry);

            if (entry != null)
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
}