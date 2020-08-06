using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class GnuArExtractor : ExtractorImplementation
    {
        public GnuArExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.AR;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an archive file created with GNU ar
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        ///         
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
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
                if (options.Parallel)
                {
                    var tempStore = new ConcurrentStack<FileEntry>();
                    var selectedEntries = fileEntries.Take(options.BatchSize);
                    selectedEntries.AsParallel().ForAll(arEntry =>
                    {
                        var entries = Context.ExtractFile(arEntry, options, governor);
                        if (entries.Any())
                        {
                            tempStore.PushRange(entries.ToArray());
                        }
                    });

                    fileEntries = fileEntries.Skip(selectedEntries.Count());

                    while (tempStore.TryPop(out var result))
                    {
                        if (result != null)
                            yield return result;
                    }
                }
                else
                {
                    foreach (var entry in fileEntries)
                    {
                        foreach (var extractedFile in Context.ExtractFile(entry, options, governor))
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
