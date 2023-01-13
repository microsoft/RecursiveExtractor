﻿using DiscUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The WIM Image Extractor Implementation
    /// </summary>
    public class WimExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public WimExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts a WIM file contained in fileEntry.
        /// </summary>
                ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            DiscUtils.Wim.WimFile? baseFile = null;
            try
            {
                baseFile = new DiscUtils.Wim.WimFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to init WIM image.");
            }
            if (baseFile != null)
            {
                for (var i = 0; i < baseFile.ImageCount; i++)
                {
                    var image = baseFile.GetImage(i);
                    foreach (var file in image.GetFiles(image.Root.FullName, "*.*", SearchOption.AllDirectories))
                    {
                        Stream? stream = null;
                        try
                        {
                            var info = image.GetFileInfo(file);
                            stream = info.OpenRead();
                            governor.CheckResourceGovernor(info.Length);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Error reading {0} from WIM {1} ({2}:{3})", file, image.FriendlyName, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var name = file.Replace('\\', Path.DirectorySeparatorChar);
                            var newFileEntry = await FileEntry.FromStreamAsync($"{image.FriendlyName}{Path.DirectorySeparatorChar}{name}", stream, fileEntry, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);

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
                            stream.Dispose();
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

        /// <summary>
        ///     Extracts a WIM file contained in fileEntry.
        /// </summary>
                ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            DiscUtils.Wim.WimFile? baseFile = null;
            try
            {
                baseFile = new DiscUtils.Wim.WimFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to init WIM image.");
            }
            if (baseFile != null)
            {
                for (var i = 0; i < baseFile.ImageCount; i++)
                {
                    var image = baseFile.GetImage(i);
                    foreach (var file in image.GetFiles(image.Root.FullName, "*.*", SearchOption.AllDirectories))
                    {
                        Stream? stream = null;
                        try
                        {
                            var info = image.GetFileInfo(file);
                            stream = info.OpenRead();
                            governor.CheckResourceGovernor(info.Length);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Error reading {0} from WIM {1} ({2}:{3})", file, image.FriendlyName, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var name = file.Replace('\\', Path.DirectorySeparatorChar);

                            var newFileEntry = new FileEntry($"{image.FriendlyName}{Path.DirectorySeparatorChar}{name}", stream, fileEntry, memoryStreamCutoff: options.MemoryStreamCutoff);
                            if (options.Recurse || topLevel)
                            {
                                foreach (var extractedFile in Context.Extract(newFileEntry, options, governor, false))
                                {
                                    yield return extractedFile;
                                }
                            }
                            else
                            {
                                yield return newFileEntry;
                            }
                            stream.Dispose();
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