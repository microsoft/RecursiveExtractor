using System;
using System.Collections.Generic;
using System.IO;
using NLog;
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
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            using TarArchive archive = TarArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions()
            {
                LeaveStreamOpen = true
            });
            if (archive is null)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, "Null Archive");
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
            else
            {
                List<FileEntry> entries = new();

                TarEntryCollectionEnumerator tarEntryCollectionEnumerator = new(archive.Entries, fileEntry.FullPath);
                while (tarEntryCollectionEnumerator.GetNextEntry() != null)
                {
                    var tarEntry = tarEntryCollectionEnumerator.CurrentEntry;
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }
                    var fs = StreamFactory.GenerateAppropriateBackingStream(options, tarEntry.Size);
                    governor.CheckResourceGovernor(tarEntry.Size);
                    try
                    {
                        using Stream tarStream = tarEntry.OpenEntryStream();
                        tarStream.CopyTo(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Key, e.GetType());
                    }
                    var name = tarEntry.Key?.Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrEmpty(name))
                    {
                        Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.TAR, fileEntry.FullPath);
                        continue;
                    }
                    // Remove leading ./
                    while (name.StartsWith($".{Path.DirectorySeparatorChar}"))
                    {
                        name = name[2..];
                    }
                    
                    var newFileEntry = new FileEntry(name, fs, fileEntry, true, memoryStreamCutoff: options.MemoryStreamCutoff);

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
            }
        }

        /// <summary>
        ///     Extracts a Tar archive
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            using TarArchive archive = TarArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions()
            {
                LeaveStreamOpen = true
            });
            if (archive is null)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, "Null Archive");
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
            else
            {
                TarEntryCollectionEnumerator tarEntryCollectionEnumerator = new(archive.Entries, fileEntry.FullPath);
                while(tarEntryCollectionEnumerator.GetNextEntry() != null)
                {
                    var tarEntry = tarEntryCollectionEnumerator.CurrentEntry;
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }

                    var fs = StreamFactory.GenerateAppropriateBackingStream(options, tarEntry.Size);
                    governor.CheckResourceGovernor(tarEntry.Size);
                    try
                    {
                        using Stream tarStream = tarEntry.OpenEntryStream();
                        tarStream.CopyTo(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Key, e.GetType());
                    }
                    var name = tarEntry.Key?.Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrEmpty(name))
                    {
                        Logger.Debug(Extractor.ENTRY_MISSING_NAME_ERROR_MESSAGE_STRING, ArchiveFileType.TAR, fileEntry.FullPath);
                        continue;
                    }
                    // Remove leading ./
                    while (name.StartsWith($".{Path.DirectorySeparatorChar}"))
                    {
                        name = name[2..];
                    }
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

        private class TarEntryCollectionEnumerator
        {
            private ICollection<TarArchiveEntry> _entries;
            private IEnumerator<TarArchiveEntry> _enumerator;
            private string _fileName;
            private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

            internal TarArchiveEntry CurrentEntry => _enumerator.Current;

            internal TarEntryCollectionEnumerator(ICollection<TarArchiveEntry> entries, string fileName)
            {
                _entries = entries;
                _enumerator = _entries.GetEnumerator();
                _fileName = fileName;
            }

            internal TarArchiveEntry? GetNextEntry()
            {
                try
                {
                    if (_enumerator.MoveNext())
                    {
                        return _enumerator.Current;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch(SharpCompress.Common.IncompleteArchiveException)
                {
                    Logger.Debug("Encountered incomplete tar archive {0}, skipping further extraction of this archive.", _fileName);
                    return null;
                }
            }
        }

        private const int bufferSize = 4096;
    }
}