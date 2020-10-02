using DiscUtils;
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
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
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
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    for (var i = 0; i < baseFile.ImageCount; i++)
                    {
                        var image = baseFile.GetImage(i);
                        var fileList = image.GetFiles(image.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();
                        while (fileList.Count > 0)
                        {
                            var batchSize = Math.Min(options.BatchSize, fileList.Count);
                            var range = fileList.Take(batchSize);
                            var streamsAndNames = new List<(DiscFileInfo, Stream)>();
                            foreach (var file in range)
                            {
                                try
                                {
                                    var info = image.GetFileInfo(file);
                                    var read = info.OpenRead();
                                    streamsAndNames.Add((info, read));
                                }
                                catch (Exception e)
                                {
                                    Logger.Debug("Error reading {0} from WIM {1} ({2}:{3})", file, image.FriendlyName, e.GetType(), e.Message);
                                }
                            }
                            governor.CheckResourceGovernor(streamsAndNames.Sum(x => x.Item1.Length));
                            streamsAndNames.AsParallel().ForAll(file =>
                            {
                                var newFileEntry = new FileEntry($"{image.FriendlyName}\\{file.Item1.FullName}", file.Item2, fileEntry);
                                var entries = Context.Extract(newFileEntry, options, governor);
                                if (entries.Any())
                                {
                                    files.PushRange(entries.ToArray());
                                }
                            });
                            fileList.RemoveRange(0, batchSize);

                            while (files.TryPop(out var result))
                            {
                                if (result != null)
                                    yield return result;
                            }
                        }
                    }
                }
                else
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
                                var newFileEntry = await FileEntry.FromStreamAsync($"{image.FriendlyName}{Path.DirectorySeparatorChar}{name}", stream, fileEntry);
                                await foreach (var entry in Context.ExtractAsync(newFileEntry, options, governor))
                                {
                                    yield return entry;
                                }
                                stream.Dispose();
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

        /// <summary>
        ///     Extracts a WIM file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
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

                            var newFileEntry = new FileEntry($"{image.FriendlyName}{Path.DirectorySeparatorChar}{name}", stream, fileEntry);
                            foreach (var entry in Context.Extract(newFileEntry, options, governor))
                            {
                                yield return entry;
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
                    yield return fileEntry;
                }
            }
        }

    }
}
