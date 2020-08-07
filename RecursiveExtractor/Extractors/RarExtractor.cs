using SharpCompress.Archives.Rar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class RarExtractor : AsyncExtractorInterface
    {
        public RarExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an a RAR archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            RarArchive? rarArchive = null;
            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (rarArchive != null)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory && !x.IsEncrypted);
                foreach (var entry in entries)
                {
                    governor.CheckResourceGovernor(entry.Size);
                    var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                    FileEntry newFileEntry = await FileEntry.FromStreamAsync(name, entry.OpenEntryStream(), fileEntry);
                    if (newFileEntry != null)
                    {
                        if (Extractor.IsQuine(newFileEntry))
                        {
                            Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        await foreach (var extractedFile in Context.ExtractFileAsync(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
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

        /// <summary>
        ///     Extracts an a RAR archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            RarArchive? rarArchive = null;
            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (rarArchive != null)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory && !x.IsEncrypted);
                foreach (var entry in entries)
                {
                    governor.CheckResourceGovernor(entry.Size);
                    FileEntry? newFileEntry = null;
                    try
                    {
                        var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);

                        newFileEntry = new FileEntry(name, entry.OpenEntryStream(), fileEntry);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        if (Extractor.IsQuine(newFileEntry))
                        {
                            Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        foreach (var extractedFile in Context.ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
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
