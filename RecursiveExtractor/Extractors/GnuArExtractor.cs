using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class GnuArExtractor : AsyncExtractorInterface
    {
        public GnuArExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an archive file created with GNU ar
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        ///         
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            await foreach (var entry in ArFile.GetFileEntriesAsync(fileEntry, options, governor))
            {
                await foreach (var extractedFile in Context.ExtractFileAsync(entry, options, governor))
                {
                    yield return extractedFile;
                }
            }
        }

        /// <summary>
        ///     Extracts an archive file created with GNU ar
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        ///         
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
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
                
                    foreach (var entry in fileEntries)
                    {
                        foreach (var extractedFile in Context.ExtractFile(entry, options, governor))
                        {
                            yield return extractedFile;
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
