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
                ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
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
                var newFileEntry = new FileEntry(newFilename, xzStream, fileEntry, memoryStreamCutoff: options.MemoryStreamCutoff);

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
                xzStream.Dispose();
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
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
                ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
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
                var newFileEntry = new FileEntry(newFilename, xzStream, fileEntry, memoryStreamCutoff: options.MemoryStreamCutoff);

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
                xzStream.Dispose();
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