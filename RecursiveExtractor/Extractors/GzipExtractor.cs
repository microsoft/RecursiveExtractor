using SharpCompress.Archives.GZip;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class GzipExtractor : ExtractorImplementation
    {
        public GzipExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.GZIP;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an Gzip file contained in fileEntry. Since this function is recursive, even though
        ///     Gzip only supports a single compressed file, that inner file could itself contain multiple others.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            GZipArchive? gzipArchive = null;
            try
            {
                gzipArchive = GZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.GZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (gzipArchive != null)
            {
                foreach (var entry in gzipArchive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        continue;
                    }

                    governor.CheckResourceGovernor(entry.Size);

                    var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                    if (fileEntry.Name.EndsWith(".tgz", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newFilename = newFilename[0..^4] + ".tar";
                    }

                    FileEntry? newFileEntry = null;
                    try
                    {
                        using var stream = entry.OpenEntryStream();
                        newFileEntry = new FileEntry(newFilename, stream, fileEntry);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.GZIP, fileEntry.FullPath, newFilename, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        foreach (var extractedFile in Context.ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
                    }
                }
                gzipArchive.Dispose();
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
