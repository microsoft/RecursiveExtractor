using SharpCompress.Archives.Rar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class RarExtractor : ExtractorImplementation
    {
        public RarExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.RAR;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an a RAR archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
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
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Any())
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count());

                        var streams = entries.Take(batchSize).Select(entry => (entry, entry.OpenEntryStream())).ToList();

                        governor.CheckResourceGovernor(streams.Sum(x => x.Item2.Length));

                        streams.AsParallel().ForAll(streampair =>
                        {
                            try
                            {
                                var newFileEntry = new FileEntry(streampair.entry.Key, streampair.Item2, fileEntry);
                                if (Extractor.IsQuine(newFileEntry))
                                {
                                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                    governor.CurrentOperationProcessedBytesLeft = -1;
                                }
                                else
                                {
                                    files.PushRange(Context.ExtractFile(newFileEntry, options, governor).ToArray());
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, streampair.entry.Key, e.GetType());
                            }
                        });
                        governor.CheckResourceGovernor(0);

                        entries = entries.Skip(streams.Count);

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
                        FileEntry? newFileEntry = null;
                        try
                        {
                            newFileEntry = new FileEntry(entry.Key, entry.OpenEntryStream(), fileEntry);
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
