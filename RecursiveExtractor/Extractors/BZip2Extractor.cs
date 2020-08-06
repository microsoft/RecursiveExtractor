using SharpCompress.Compressors.BZip2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class BZip2Extractor : AsyncExtractorInterface
    {
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
            BZip2Stream? bzip2Stream = null;
            try
            {
                bzip2Stream = new BZip2Stream(fileEntry.Content, SharpCompress.Compressors.CompressionMode.Decompress, false);
                governor.CheckResourceGovernor(bzip2Stream.Length);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.BZIP2, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (bzip2Stream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = await FileEntry.FromStreamAsync(newFilename, bzip2Stream, fileEntry);

                if (Extractor.IsQuine(newFileEntry))
                {
                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    bzip2Stream.Dispose();
                    throw new OverflowException();
                }

                await foreach (var extractedFile in Context.ExtractFileAsync(newFileEntry, options, governor))
                {
                    yield return extractedFile;
                }
                bzip2Stream.Dispose();
            }
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    yield return fileEntry;
                }
            }
        }


        ///     Extracts an BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            BZip2Stream? bzip2Stream = null;
            try
            {
                bzip2Stream = new BZip2Stream(fileEntry.Content, SharpCompress.Compressors.CompressionMode.Decompress, false);
                governor.CheckResourceGovernor(bzip2Stream.Length);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.BZIP2, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (bzip2Stream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, bzip2Stream, fileEntry);

                if (Extractor.IsQuine(newFileEntry))
                {
                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    bzip2Stream.Dispose();
                    throw new OverflowException();
                }

                foreach (var extractedFile in Context.ExtractFile(newFileEntry, options, governor))
                {
                    yield return extractedFile;
                }
                bzip2Stream.Dispose();
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
