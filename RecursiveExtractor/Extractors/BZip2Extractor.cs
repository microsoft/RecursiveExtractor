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
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            try
            {
                BZip2.Decompress(fileEntry.Content, fs, false);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, "BZip2", e.GetType(), e.Message, e.StackTrace);
                yield break;
            }
            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);

            var entry = await FileEntry.FromStreamAsync(newFilename, fs, fileEntry);

            if (entry != null)
            {

                if (Extractor.IsQuine(entry))
                {
                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                await foreach (var extractedFile in Context.ExtractAsync(entry, options, governor))
                {
                    yield return extractedFile;
                }
            }
        }

        ///     Extracts a BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);

            try
            {
                BZip2.Decompress(fileEntry.Content, fs, false);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, "BZip2", e.GetType(), e.Message, e.StackTrace);
                yield break;
            }
            var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);

            var entry = new FileEntry(newFilename, fs, fileEntry);

            if (entry != null)
            {
                if (Extractor.IsQuine(entry))
                {
                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                foreach (var extractedFile in Context.Extract(entry, options, governor))
                {
                    yield return extractedFile;
                }
            }
        }
    }
}
