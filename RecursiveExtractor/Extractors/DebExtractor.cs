using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class DebExtractor : ExtractorImplementation
    {
        public DebExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.DEB;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }


        /// <summary>
        ///     Extracts a .deb file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            IEnumerable<FileEntry>? entries = null;
            try
            {
                entries = DebArchiveFile.GetFileEntries(fileEntry, options, governor);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.DEB, fileEntry.FullPath, string.Empty, e.GetType());
                if (e is OverflowException)
                {
                    throw;
                }
            }
            if (entries != null)
            {
                foreach (var entry in entries)
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
