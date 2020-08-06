using NLog;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class SevenZipExtractor : ExtractorImplementation
    {
        public SevenZipExtractor(Extractor extractor)
        {
            Extractor = extractor;
            TargetType = ArchiveFileType.P7ZIP;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Extractor { get; }


        /// <summary>
        ///     Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>

        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            SevenZipArchive? sevenZipArchive = null;
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (sevenZipArchive != null)
            {
                var entries = sevenZipArchive.Entries.Where(x => !x.IsDirectory && !x.IsEncrypted && x.IsComplete).ToList();

                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Count() > 0)
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count());
                        var selectedEntries = entries.GetRange(0, batchSize).Select(entry => (entry, entry.OpenEntryStream()));
                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.entry.Size));

                        try
                        {
                            selectedEntries.AsParallel().ForAll(entry =>
                            {
                                try
                                {
                                    var newFileEntry = new FileEntry(entry.entry.Key, entry.Item2, fileEntry);
                                    if (Extractor.IsQuine(newFileEntry))
                                    {
                                        Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                        governor.CurrentOperationProcessedBytesLeft = -1;
                                    }
                                    else
                                    {
                                        files.PushRange(Extractor.ExtractFile(newFileEntry, options, governor).ToArray());
                                    }
                                }
                                catch (Exception e) when (e is OverflowException)
                                {
                                    Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.entry.Key, e.GetType());
                                    throw;
                                }
                                catch (Exception e)
                                {
                                    Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.entry.Key, e.GetType());
                                }
                            });
                        }
                        catch (Exception e) when (e is AggregateException)
                        {
                            if (e.InnerException?.GetType() == typeof(OverflowException))
                            {
                                throw e.InnerException;
                            }
                            throw;
                        }

                        governor.CheckResourceGovernor(0);
                        entries.RemoveRange(0, batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        governor.CheckResourceGovernor(entry.Size);
                        var newFileEntry = new FileEntry(entry.Key, entry.OpenEntryStream(), fileEntry);

                        if (Extractor.IsQuine(newFileEntry))
                        {
                            Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        foreach (var extractedFile in Extractor.ExtractFile(newFileEntry, options, governor))
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
