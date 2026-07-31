using DiscUtils.Udf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The UDF disc image extractor implementation.
    /// </summary>
    public class UdfExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public UdfExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an UDF file
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            DiscUtils.DiscFileInfo[]? entries = null;
            Dictionary<string, FileEntryMetadata>? metadataByPath = null;
            var failed = false;
            try
            {
                using var cd = new UdfReader(fileEntry.Content);
                entries = cd.Root.GetFiles("*.*", SearchOption.AllDirectories).ToArray();
                metadataByPath = DiscCommon.CollectMetadata(cd, entries);
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to open UDF {0}. ({1}:{2})", fileEntry.FullPath, e.GetType(), e.Message);
                failed = true;
            }
            if (failed)
            {
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
            else if (entries != null)
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
                        Logger.Debug("Failed to extract {0} from UDF {1}. ({2}:{3})", fileInfo.FullName, fileEntry.FullPath, e.GetType(), e.Message);
                    }
                    if (stream != null)
                    {
                        var name = fileInfo.FullName.Replace('/', Path.DirectorySeparatorChar);
                        var newFileEntry = await FileEntry.FromStreamAsync(name, stream, fileEntry, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
                        if (metadataByPath != null && metadataByPath.TryGetValue(fileInfo.FullName, out var entryMetadata))
                        {
                            newFileEntry.Metadata = entryMetadata;
                        }
                        if (options.Recurse || topLevel)
                        {
                            await foreach (var entry in Context.ExtractAsync(newFileEntry, options, governor, false))
                            {
                                yield return entry;
                            }
                        }
                        else
                        {
                            yield return newFileEntry;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Extracts an UDF file
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            DiscUtils.DiscFileInfo[]? entries = null;
            Dictionary<string, FileEntryMetadata>? metadataByPath = null;
            var failed = false;
            try
            {
                using var cd = new UdfReader(fileEntry.Content);
                entries = cd.Root.GetFiles("*.*", SearchOption.AllDirectories).ToArray();
                metadataByPath = DiscCommon.CollectMetadata(cd, entries);
            }
            catch(Exception e)
            {
                Logger.Debug("Failed to open UDF {0}. ({1}:{2})", fileEntry.FullPath, e.GetType(), e.Message);
                failed = true;
            }
            if (failed)
            {
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
            else if (entries != null)
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
                        Logger.Debug("Failed to extract {0} from UDF {1}. ({2}:{3})", fileInfo.FullName, fileEntry.FullPath, e.GetType(), e.Message);
                    }
                    if (stream != null)
                    {
                        var name = fileInfo.FullName.Replace('/', Path.DirectorySeparatorChar);
                        var newFileEntry = new FileEntry(name, stream, fileEntry, createTime: file.CreationTime, modifyTime: file.LastWriteTime, accessTime: file.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                        if (metadataByPath != null && metadataByPath.TryGetValue(fileInfo.FullName, out var entryMetadata))
                        {
                            newFileEntry.Metadata = entryMetadata;
                        }
                        if (options.Recurse || topLevel)
                        {
                            foreach (var entry in Context.Extract(newFileEntry, options, governor, false))
                            {
                                yield return entry;
                            }
                        }
                        else
                        {
                            yield return newFileEntry;
                        }
                    }
                }
            }
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                    yield return fileEntry;
                }
            }
        }
    }
}
