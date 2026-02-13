using DiscUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        /// Tries to extract file metadata from a DiscUtils file system entry.
        /// For file systems implementing <see cref="IUnixFileSystem"/> (Ext, Xfs, Btrfs, HfsPlus),
        /// returns permissions, UID, and GID. Returns null for unsupported file systems.
        /// </summary>
        /// <param name="fs">The opened disc file system</param>
        /// <param name="filePath">Path of the file within the file system</param>
        /// <returns>Populated <see cref="FileEntryMetadata"/> or null when not available</returns>
        internal static FileEntryMetadata? TryGetFileMetadata(DiscFileSystem fs, string filePath)
        {
            if (fs is not IUnixFileSystem unixFs)
            {
                return null;
            }

            try
            {
                var info = unixFs.GetUnixFileInfo(filePath);
                return new FileEntryMetadata
                {
                    Mode = (long)info.Permissions,
                    Uid = info.UserId,
                    Gid = info.GroupId
                };
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Could not retrieve Unix metadata for {0}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Pre-collects metadata for all files while the file system is still open.
        /// Used by extractors (e.g., ISO) where the file system is disposed before files are processed.
        /// </summary>
        /// <param name="fs">The opened disc file system</param>
        /// <param name="fileInfos">The file entries to collect metadata for</param>
        /// <returns>A dictionary mapping file paths to metadata, or null if the file system does not support metadata</returns>
        internal static Dictionary<string, FileEntryMetadata>? CollectMetadata(DiscFileSystem fs, DiscFileInfo[] fileInfos)
        {
            if (fs is not IUnixFileSystem)
            {
                return null;
            }

            var result = new Dictionary<string, FileEntryMetadata>();
            foreach (var fi in fileInfos)
            {
                var metadata = TryGetFileMetadata(fs, fi.FullName);
                if (metadata != null)
                {
                    result[fi.FullName] = metadata;
                }
            }
            return result;
        }

        /// <summary>
        /// Dump the FileEntries from a Logical Volume asynchronously
        /// </summary>
        /// <param name="volume">The Volume to dump</param>
        /// <param name="parentPath">The Path to the parent Disc</param>
        /// <param name="options">Extractor Options to use</param>
        /// <param name="governor">Resource Governor to use</param>
        /// <param name="Context">Extractor context to use</param>
        /// <param name="parent">The Parent FileEntry</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns></returns>
        public static async IAsyncEnumerable<FileEntry> DumpLogicalVolumeAsync(LogicalVolumeInfo volume, string parentPath, ExtractorOptions options, ResourceGovernor governor, Extractor Context, FileEntry? parent = null, bool topLevel = true)
        {
            ReadOnlyCollection<DiscUtils.FileSystemInfo>? fsInfos = null;
            try
            {
                fsInfos = FileSystemManager.DetectFileSystems(volume);
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to get file systems from logical volume {0} Image {1} ({2}:{3})", volume.Identity, parentPath, e.GetType(), e.Message);
            }

            foreach (var fsInfo in fsInfos ?? Enumerable.Empty<DiscUtils.FileSystemInfo>())
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
                        var newFileEntry = await FileEntry.FromStreamAsync($"{volume.Identity}{Path.DirectorySeparatorChar}{fi.FullName}", fileStream, parent, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
                        newFileEntry.Metadata = TryGetFileMetadata(fs, file);
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
        /// Dump the FileEntries from a Logical Volume
        /// </summary>
        /// <param name="volume">The Volume to dump</param>
        /// <param name="parentPath">The Path to the parent Disc</param>
        /// <param name="options">Extractor Options to use</param>
        /// <param name="governor">Resource Governor to use</param>
        /// <param name="Context">Extractor context to use</param>
        /// <param name="parent">The Parent FilEntry</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns>An enumerable of the contained File Entries.</returns>
        public static IEnumerable<FileEntry> DumpLogicalVolume(LogicalVolumeInfo volume, string parentPath, ExtractorOptions options, ResourceGovernor governor, Extractor Context, FileEntry? parent = null, bool topLevel = true)
        {
            ReadOnlyCollection<DiscUtils.FileSystemInfo>? fsInfos = null;
            try
            {
                fsInfos = FileSystemManager.DetectFileSystems(volume);
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to get file systems from logical volume {0} Image {1} ({2}:{3})", volume.Identity, parentPath, e.GetType(), e.Message);
            }

            foreach (var fsInfo in fsInfos ?? Enumerable.Empty<DiscUtils.FileSystemInfo>())
            {
                using var fs = fsInfo.Open(volume);
                var diskFiles = fs.GetFiles(fs.Root.FullName, "*.*", SearchOption.AllDirectories).ToList();
                
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
                        newFileEntry.Metadata = TryGetFileMetadata(fs, file);
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
                    }
                }
            }
        }
    }
}
