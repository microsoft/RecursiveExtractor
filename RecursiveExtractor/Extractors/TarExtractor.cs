using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives.Tar;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Tar archive extractor implementation
    /// </summary>
    public class TarExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public TarExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts a Tar archive
        /// </summary>
        /// <param name="fileEntry">The <see cref="FileEntry"/> to extract.</param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {

            using TarArchive archive = TarArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions()
            {
                LeaveStreamOpen = true
            });
            if (archive is null || archive.TotalSize == 0)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, "Null Archive");
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
            else
            {
                List<FileEntry> entries = new();

                foreach (TarArchiveEntry tarEntry in archive.Entries)
                {
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }

                    var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                    governor.CheckResourceGovernor(tarEntry.Size);
                    try
                    {
                        using Stream tarStream = tarEntry.OpenEntryStream();
                        tarStream.CopyTo(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Key, e.GetType());
                    }
                    var name = tarEntry.Key.Replace('/', Path.DirectorySeparatorChar);
                    var newFileEntry = new FileEntry(name, fs, fileEntry, true, memoryStreamCutoff: options.MemoryStreamCutoff);

                    if (newFileEntry is not null)
                    {
                        entries.Add(newFileEntry);
                    }
                }
                foreach (var entry in entries)
                {
                    if (options.Recurse || topLevel)
                    {
                        await foreach (var innerEntry in Context.ExtractAsync(entry, options, governor, false))
                        {
                            yield return innerEntry;
                        }
                    }
                    else
                    {
                        yield return entry;
                    }
                }
            }
        }

        /// <summary>
        ///     Extracts a Tar archive
        /// </summary>
        /// <param name="fileEntry">The <see cref="FileEntry"/> to extract.</param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns> </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            using TarArchive archive = TarArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions()
            {
                LeaveStreamOpen = true
            });
            if (archive is null || archive.TotalSize == 0)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, "Null Archive");
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
            else
            {
                foreach (TarArchiveEntry tarEntry in archive.Entries)
                {
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }

                    var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                    governor.CheckResourceGovernor(tarEntry.Size);
                    try
                    {
                        using Stream tarStream = tarEntry.OpenEntryStream();
                        tarStream.CopyTo(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Key, e.GetType());
                    }
                    var name = tarEntry.Key.Replace('/', Path.DirectorySeparatorChar);
                    var newFileEntry = new FileEntry(name, fs, fileEntry, true, memoryStreamCutoff: options.MemoryStreamCutoff);

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
                }
            }
        }

        private const int bufferSize = 4096;
    }
}