﻿using DiscUtils.Iso9660;
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
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            DiscUtils.DiscFileInfo[]? entries = null;
            var failed = false;
            try
            {
                using var cd = new CDReader(fileEntry.Content, true);
                entries = cd.Root.GetFiles("*.*", SearchOption.AllDirectories).ToArray();
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to open ISO {0}. ({1}:{2})", fileEntry.FullPath, e.GetType(), e.Message);
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
                        Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.FullName, fileEntry.FullPath, e.GetType(), e.Message);
                    }
                    if (stream != null)
                    {
                        var name = fileInfo.FullName.Replace('/', Path.DirectorySeparatorChar);
                        var newFileEntry = await FileEntry.FromStreamAsync(name, stream, fileEntry, fileInfo.CreationTime, fileInfo.LastWriteTime, fileInfo.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
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
        ///     Extracts an ISO file
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            DiscUtils.DiscFileInfo[]? entries = null;
            var failed = false;
            try
            {
                using var cd = new CDReader(fileEntry.Content, true);
                entries = cd.Root.GetFiles("*.*", SearchOption.AllDirectories).ToArray();
            }
            catch(Exception e)
            {
                Logger.Debug("Failed to open ISO {0}. ({1}:{2})", fileEntry.FullPath, e.GetType(), e.Message);
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
                        Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.FullName, fileEntry.FullPath, e.GetType(), e.Message);
                    }
                    if (stream != null)
                    {
                        var name = fileInfo.FullName.Replace('/', Path.DirectorySeparatorChar);
                        var newFileEntry = new FileEntry(name, stream, fileEntry, createTime: file.CreationTime, modifyTime: file.LastWriteTime, accessTime: file.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff);
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