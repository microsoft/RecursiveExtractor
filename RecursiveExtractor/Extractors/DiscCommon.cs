using DiscUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// Common crawler for some disc formats
    /// </summary>
    public static class DiscCommon
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Dump the FileEntries from a Logical Volume asynchronously
        /// </summary>
        /// <param name="volume">The Volume to dump</param>
        /// <param name="parentPath">The Path to the parent Disc</param>
        /// <param name="options">Extractor Options to use</param>
        /// <param name="governor">Resource Governor to use</param>
        /// <param name="Context">Extractor context to use</param>
        /// <param name="parent">The Parent FilEntry</param>
        /// <returns></returns>
        public static async IAsyncEnumerable<FileEntry> DumpLogicalVolumeAsync(LogicalVolumeInfo volume, string parentPath, ExtractorOptions options, ResourceGovernor governor, Extractor Context, FileEntry? parent = null)
        {
            DiscUtils.FileSystemInfo[]? fsInfos = null;
            try
            {
                fsInfos = FileSystemManager.DetectFileSystems(volume);
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to get file systems from logical volume {0} Image {1} ({2}:{3})", volume.Identity, parentPath, e.GetType(), e.Message);
            }

            foreach (var fsInfo in fsInfos ?? Array.Empty<DiscUtils.FileSystemInfo>())
            {
                using var fs = fsInfo.Open(volume);
                var diskFiles = fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();

                foreach (var file in diskFiles)
                {
                    Stream? fileStream = null;
                    DiscFileInfo? fi = null;
                    try
                    {
                        fi = fs.GetFileInfo(file);
                        governor.CheckResourceGovernor(fi.Length);
                        fileStream = fi.OpenRead();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                    }
                    if (fileStream != null && fi != null)
                    {
                        var newFileEntry = await FileEntry.FromStreamAsync($"{volume.Identity}{Path.DirectorySeparatorChar}{fi.FullName}", fileStream, parent, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                        var entries = Context.ExtractAsync(newFileEntry, options, governor);
                        await foreach (var entry in entries)
                        {
                            yield return entry;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dump the FileEntries from a Logical Volume
        /// </summary>
        /// <param name="volume">The Volume to dump</param>
        /// <param name="parentPath">The Path to the parent Disc</param>
        /// <param name="options">Extractor Options to use</param>
        /// <param name="governor">Resource Governor to use</param>
        /// <param name="Context">Extractor context to use</param>
        /// <param name="parent">The Parent FilEntry</param>
        /// <returns></returns>
        public static IEnumerable<FileEntry> DumpLogicalVolume(LogicalVolumeInfo volume, string parentPath, ExtractorOptions options, ResourceGovernor governor, Extractor Context, FileEntry? parent = null)
        {
            DiscUtils.FileSystemInfo[]? fsInfos = null;
            try
            {
                fsInfos = FileSystemManager.DetectFileSystems(volume);
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to get file systems from logical volume {0} Image {1} ({2}:{3})", volume.Identity, parentPath, e.GetType(), e.Message);
            }

            foreach (var fsInfo in fsInfos ?? Array.Empty<DiscUtils.FileSystemInfo>())
            {
                using var fs = fsInfo.Open(volume);
                var diskFiles = fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (diskFiles.Any())
                    {
                        var batchSize = Math.Min(options.BatchSize, diskFiles.Count);
                        var range = diskFiles.GetRange(0, batchSize);
                        var fileinfos = new List<(string name, DateTime created, DateTime modified, DateTime accessed, Stream stream)>();
                        long totalLength = 0;
                        foreach (var r in range)
                        {
                            try
                            {
                                var fi = fs.GetFileInfo(r);
                                totalLength += fi.Length;
                                fileinfos.Add((fi.FullName, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, fi.OpenRead()));
                            }
                            catch (Exception e)
                            {
                                Logger.Debug("Failed to get FileInfo from {0} in Volume {1} @ {2} ({3}:{4})", r, volume.Identity, parentPath, e.GetType(), e.Message);
                            }
                        }

                        governor.CheckResourceGovernor(totalLength);

                        fileinfos.AsParallel().ForAll(file =>
                        {
                            if (file.stream != null)
                            {
                                var newFileEntry = new FileEntry($"{volume.Identity}{Path.DirectorySeparatorChar}{file.name}", file.stream, parent, false, file.created, file.modified, file.accessed, memoryStreamCutoff: options.MemoryStreamCutoff);
                                var entries = Context.Extract(newFileEntry, options, governor);
                                files.PushRange(entries.ToArray());
                            }
                        });
                        diskFiles.RemoveRange(0, batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (var file in diskFiles)
                    {
                        Stream? fileStream = null;
                        (DateTime? creation, DateTime? modification, DateTime? access) = (null, null, null);
                        try
                        {
                            var fi = fs.GetFileInfo(file);
                            governor.CheckResourceGovernor(fi.Length);
                            fileStream = fi.OpenRead();
                            creation = fi.CreationTime;
                            modification = fi.LastWriteTime;
                            access = fi.LastAccessTime;
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                        }
                        if (fileStream != null)
                        {
                            var newFileEntry = new FileEntry($"{volume.Identity}{Path.DirectorySeparatorChar}{file}", fileStream, parent, false, creation, modification, access, memoryStreamCutoff: options.MemoryStreamCutoff);
                            var entries = Context.Extract(newFileEntry, options, governor);
                            foreach (var entry in entries)
                            {
                                yield return entry;
                            }
                        }
                    }
                }
            }
        }
    }
}
