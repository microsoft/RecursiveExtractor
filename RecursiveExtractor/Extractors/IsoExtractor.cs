﻿using DiscUtils;
using DiscUtils.Iso9660;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The ISO disc image extractor implementation.
    /// </summary>
    public class IsoExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public IsoExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an ISO file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var cd = new CDReader(fileEntry.Content, true);
            var entries = cd.Root.GetFiles("*.*", SearchOption.AllDirectories).Where(x => options.FileNamePasses($"{fileEntry.FullPath}{Path.DirectorySeparatorChar}{x.FullName}")).ToArray();
            if (entries != null)
            {
                foreach (var file in entries)
                {
                    var fileInfo = file;
                    governor.CheckResourceGovernor(fileInfo.Length);
                    Stream? stream = null;
                    try
                    {
                        stream = fileInfo.OpenRead();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.FullName, fileEntry.FullPath, e.GetType(), e.Message);
                    }
                    if (stream != null)
                    {
                        var name = fileInfo.FullName.Replace('/', Path.DirectorySeparatorChar);
                        var newFileEntry = await FileEntry.FromStreamAsync(name, stream, fileEntry, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
                        var innerEntries = Context.ExtractAsync(newFileEntry, options, governor);
                        await foreach (var entry in innerEntries)
                        {
                            yield return entry;
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
        ///     Extracts an ISO file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var cd = new CDReader(fileEntry.Content, true);
            var entries = cd.Root.GetFiles("*.*", SearchOption.AllDirectories).Where(x => options.FileNamePasses($"{fileEntry.FullPath}{Path.DirectorySeparatorChar}{x.FullName}")).ToArray();
            if (entries != null)
            {
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    var batchSize = Math.Min(options.BatchSize, entries.Length);
                    while(entries.Length > 0)
                    {
                        var selectedFileEntries = entries.Take(batchSize);
                        var fileInfoTuples = new List<(string name, DateTime created, DateTime modified, DateTime accessed, Stream stream)>();

                        foreach (var selectedFileEntry in selectedFileEntries)
                        {
                            try
                            {
                                var stream = selectedFileEntry.OpenRead();

                                fileInfoTuples.Add((selectedFileEntry.FullName.Replace('/', Path.DirectorySeparatorChar), selectedFileEntry.CreationTime, selectedFileEntry.LastWriteTime, selectedFileEntry.LastAccessTime, stream));
                            }
                            catch (Exception e)
                            {
                                Logger.Debug("Failed to get FileInfo or OpenStream from {0} in ISO {1} ({2}:{3})", selectedFileEntry, fileEntry.FullPath, e.GetType(), e.Message);
                            }
                        }

                        governor.CheckResourceGovernor(fileInfoTuples.Sum(x => x.stream.Length));

                        fileInfoTuples.AsParallel().ForAll(cdFile =>
                        {
                            var newFileEntry = new FileEntry(cdFile.name, cdFile.stream, fileEntry, false, cdFile.created, cdFile.modified, cdFile.accessed, memoryStreamCutoff: options.MemoryStreamCutoff);
                            var entries = Context.Extract(newFileEntry, options, governor);
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
                }
                else
                {
                    foreach (var file in entries)
                    {
                        var fileInfo = file;
                        governor.CheckResourceGovernor(fileInfo.Length);
                        Stream? stream = null;
                        try
                        {
                            stream = fileInfo.OpenRead();
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.FullName, fileEntry.FullPath, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var name = fileInfo.FullName.Replace('/', Path.DirectorySeparatorChar);
                            var newFileEntry = new FileEntry(name, stream, fileEntry, createTime: file.CreationTime, modifyTime: file.LastWriteTime, accessTime: file.LastAccessTime,memoryStreamCutoff: options.MemoryStreamCutoff);
                            foreach (var entry in Context.Extract(newFileEntry, options, governor))
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
