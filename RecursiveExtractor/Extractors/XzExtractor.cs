using SharpCompress.Compressors.Xz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The XZ Extractor Implementation
    /// </summary>
    public class XzExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public XzExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            XZStream? xzStream = null;
            try
            {
                xzStream = new XZStream(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.XZ, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (xzStream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, xzStream, fileEntry);

                // SharpCompress does not expose metadata without a full read, so we need to decompress first,
                // and then abort if the bytes exceeded the governor's capacity.

                var streamLength = xzStream.Index?.Records?.Select(r => r.UncompressedSize)
                                          .Aggregate((ulong?)0, (a, b) => a + b);

                // BUG: Technically, we're casting a ulong to a long, but we don't expect 9 exabyte steams, so
                // low risk.
                if (streamLength.HasValue)
                {
                    governor.CheckResourceGovernor((long)streamLength.Value);
                }

                if (Extractor.IsQuine(newFileEntry))
                {
                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                await foreach (var extractedFile in Context.ExtractAsync(newFileEntry, options, governor))
                {
                    yield return extractedFile;
                }
                xzStream.Dispose();
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
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            XZStream? xzStream = null;
            try
            {
                xzStream = new XZStream(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.XZ, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (xzStream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, xzStream, fileEntry);

                // SharpCompress does not expose metadata without a full read, so we need to decompress first,
                // and then abort if the bytes exceeded the governor's capacity.

                var streamLength = xzStream.Index?.Records?.Select(r => r.UncompressedSize)
                                          .Aggregate((ulong?)0, (a, b) => a + b);

                // BUG: Technically, we're casting a ulong to a long, but we don't expect 9 exabyte steams, so
                // low risk.
                if (streamLength.HasValue)
                {
                    governor.CheckResourceGovernor((long)streamLength.Value);
                }

                if (Extractor.IsQuine(newFileEntry))
                {
                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                foreach (var extractedFile in Context.Extract(newFileEntry, options, governor))
                {
                    yield return extractedFile;
                }
                xzStream.Dispose();
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
