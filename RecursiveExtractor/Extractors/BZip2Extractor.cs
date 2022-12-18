using ICSharpCode.SharpZipLib.BZip2;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The implementation for BZip Archives
    /// </summary>
    public class BZip2Extractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public BZip2Extractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            using var fs = new FileStream(TempPath.GetTempFilePath(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            var failed = false;
            try
            {
                BZip2.Decompress(fileEntry.Content, fs, false);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, "BZip2", e.GetType(), e.Message, e.StackTrace);
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

            var entry = await FileEntry.FromStreamAsync(newFilename, fs, fileEntry).ConfigureAwait(false);

            if (entry != null)
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
        ///     Extracts a BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);

            using var fs = new FileStream(TempPath.GetTempFilePath(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);

            var failed = false;

            try
            {
                BZip2.Decompress(fileEntry.Content, fs, false);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, "BZip2", e.GetType(), e.Message, e.StackTrace);
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