using DiscUtils;
using DiscUtils.Iso9660;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class IsoExtractor : ExtractorImplementation
    {
        public IsoExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.ISO_9660;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an an ISO file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var cd = new CDReader(fileEntry.Content, true);
            var entries = cd.GetFiles(cd.Root.FullName, "*.*", SearchOption.AllDirectories);
            if (entries != null)
            {
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    var batchSize = Math.Min(options.BatchSize, entries.Length);
                    var selectedFileNames = entries[0..batchSize];
                    var fileInfoTuples = new List<(DiscFileInfo, Stream)>();

                    foreach (var selectedFileName in selectedFileNames)
                    {
                        try
                        {
                            var fileInfo = cd.GetFileInfo(selectedFileName);
                            
                            var stream = fileInfo.OpenRead();

                            fileInfoTuples.Add((fileInfo, stream));
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Failed to get FileInfo or OpenStream from {0} in ISO {1} ({2}:{3})", selectedFileName, fileEntry.FullPath, e.GetType(), e.Message);
                        }
                    }

                    governor.CheckResourceGovernor(fileInfoTuples.Sum(x => x.Item1.Length));

                    fileInfoTuples.AsParallel().ForAll(cdFile =>
                    {
                        var newFileEntry = new FileEntry(cdFile.Item1.Name, cdFile.Item2, fileEntry);
                        var entries = Context.ExtractFile(newFileEntry, options, governor);
                        if (entries.Any())
                        {
                            files.PushRange(entries.ToArray());
                        }
                    });

                    entries = entries[batchSize..];

                    while (files.TryPop(out var result))
                    {
                        if (result != null)
                            yield return result;
                    }
                }
                else
                {
                    foreach (var file in entries)
                    {
                        var fileInfo = cd.GetFileInfo(file);
                        governor.CheckResourceGovernor(fileInfo.Length);
                        Stream? stream = null;
                        try
                        {
                            var fei = new FileEntryInfo(fileInfo.Name, Path.Combine(fileEntry.FullPath, fileInfo.FullName), fileInfo.Length);
                            stream = fileInfo.OpenRead();
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.Name, fileEntry.FullPath, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var newFileEntry = new FileEntry(fileInfo.Name, stream, fileEntry);
                            var innerEntries = Context.ExtractFile(newFileEntry, options, governor);
                            foreach (var entry in innerEntries)
                            {
                                yield return entry;
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
