using DiscUtils;
using DiscUtils.Iso9660;
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
        /// For file systems implementing <see cref="IUnixFileSystem"/> (such as Ext, Xfs, Btrfs, HfsPlus,
        /// and ISO 9660 images via <c>CDReader</c> when RockRidge is the active variant; see
        /// <see cref="CollectIsoMetadata"/> for images that also carry Joliet),
        /// returns permissions, UID, and GID.
        /// For file systems implementing <see cref="IDosFileSystem"/> (such as NTFS, FAT, WIM, and ISO 9660),
        /// returns Windows file attributes.
        /// For file systems implementing <see cref="IWindowsFileSystem"/> (such as NTFS and WIM),
        /// also returns the security descriptor in SDDL format when the file system provides one.
        /// The lists above are not exhaustive; support is determined by the interfaces the file system implements.
        /// Returns null for file systems that support none of these interfaces.
        /// </summary>
        /// <param name="fs">The opened disc file system</param>
        /// <param name="filePath">Path of the file within the file system</param>
        /// <returns>Populated <see cref="FileEntryMetadata"/> or null when not available</returns>
        internal static FileEntryMetadata? TryGetFileMetadata(DiscFileSystem fs, string filePath)
        {
            FileEntryMetadata? metadata = null;

            if (fs is IUnixFileSystem unixFs && SupportsUnixMetadata(fs))
            {
                metadata = ApplyUnixMetadata(unixFs, filePath, metadata);
            }

            if (fs is IDosFileSystem dosFs)
            {
                try
                {
                    var winInfo = dosFs.GetFileStandardInformation(filePath);
                    metadata ??= new FileEntryMetadata();
                    metadata.FileAttributes = winInfo.FileAttributes;
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Could not retrieve DOS file attributes for {0}", filePath);
                }
            }

            if (fs is IWindowsFileSystem windowsFs)
            {
                try
                {
                    var securityDescriptor = windowsFs.GetSecurity(filePath);
                    var sddl = securityDescriptor?.GetSddlForm(
                        DiscUtils.Core.WindowsSecurity.AccessControl.AccessControlSections.All);
                    if (!string.IsNullOrEmpty(sddl))
                    {
                        metadata ??= new FileEntryMetadata();
                        metadata.SecurityDescriptorSddl = sddl;
                    }
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Could not retrieve security descriptor for {0}", filePath);
                }
            }

            return metadata;
        }

        /// <summary>
        /// Reads the Unix mode, UID and GID for a file and applies them to <paramref name="metadata"/>,
        /// creating it when the caller does not have one yet.
        /// </summary>
        /// <param name="fs">The opened Unix file system</param>
        /// <param name="filePath">Path of the file within the file system</param>
        /// <param name="metadata">The metadata to populate, or null to create one on demand</param>
        /// <returns>The populated metadata, or the value passed in when the read failed</returns>
        private static FileEntryMetadata? ApplyUnixMetadata(IUnixFileSystem fs, string filePath, FileEntryMetadata? metadata)
        {
            try
            {
                var info = fs.GetUnixFileInfo(filePath);
                metadata ??= new FileEntryMetadata();
                metadata.Mode = (long)info.Permissions;
                metadata.Uid = info.UserId;
                metadata.Gid = info.GroupId;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Could not retrieve Unix metadata for {0}", filePath);
            }

            return metadata;
        }

        /// <summary>
        /// Determines whether a file system that implements <see cref="IUnixFileSystem"/> can actually
        /// return Unix metadata. <c>CDReader</c> implements the interface unconditionally but only exposes
        /// Unix information when the active ISO 9660 variant is RockRidge, and throws for every other
        /// variant. Checking up front avoids throwing and catching an exception for every file in an image.
        /// </summary>
        private static bool SupportsUnixMetadata(DiscFileSystem fs)
            => fs is not CDReader cdReader || cdReader.ActiveVariant == Iso9660Variant.RockRidge;

        /// <summary>
        /// Pre-collects metadata for all files while the file system is still open.
        /// Used by extractors (e.g., ISO) where the file system is disposed before files are processed.
        /// </summary>
        /// <remarks>
        /// This does not throw. An entry whose metadata cannot be read is logged and skipped, so a problem
        /// reading metadata never causes the archive itself to be reported as unreadable.
        /// </remarks>
        /// <param name="fs">The opened disc file system</param>
        /// <param name="fileInfos">The file entries to collect metadata for</param>
        /// <returns>A dictionary mapping file paths to metadata, or null if the file system does not support metadata</returns>
        internal static Dictionary<string, FileEntryMetadata>? CollectMetadata(DiscFileSystem fs, DiscFileInfo[] fileInfos)
        {
            if (fs is not IUnixFileSystem && fs is not IDosFileSystem && fs is not IWindowsFileSystem)
            {
                return null;
            }

            var result = new Dictionary<string, FileEntryMetadata>();
            foreach (var fi in fileInfos)
            {
                string? fullName = null;
                try
                {
                    fullName = fi.FullName;
                    var metadata = TryGetFileMetadata(fs, fullName);
                    if (metadata != null)
                    {
                        result[fullName] = metadata;
                    }
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Could not collect metadata for {0}", fullName ?? "an unnamed entry");
                }
            }
            return result;
        }

        /// <summary>
        /// Pre-collects metadata for the files of an ISO 9660 image while the reader is still open.
        /// </summary>
        /// <remarks>
        /// <c>CDReader</c> is opened with Joliet given priority over RockRidge, and DiscUtils only parses
        /// SUSP records for the variant it activates. On an image built with both extensions (for example
        /// <c>mkisofs -J -R</c>) the Joliet tree wins, and the RockRidge mode, UID and GID would be lost.
        /// When the active variant is not RockRidge, a second reader that skips Joliet is opened to recover
        /// them. Entries are matched by path; the two directory trees normally agree on names, and an entry
        /// that does not match simply keeps whatever the active tree provided.
        /// Like <see cref="CollectMetadata"/>, this does not throw.
        /// </remarks>
        /// <param name="cd">The opened ISO 9660 reader</param>
        /// <param name="fileInfos">The file entries to collect metadata for</param>
        /// <param name="isoStream">The stream the reader was opened over, used for the RockRidge fallback</param>
        /// <returns>A dictionary mapping file paths to metadata</returns>
        internal static Dictionary<string, FileEntryMetadata>? CollectIsoMetadata(CDReader cd, DiscFileInfo[] fileInfos, Stream isoStream)
        {
            var metadataByPath = CollectMetadata(cd, fileInfos);

            if (metadataByPath == null || cd.ActiveVariant == Iso9660Variant.RockRidge)
            {
                return metadataByPath;
            }

            try
            {
                using var rockRidgeReader = new CDReader(isoStream, false);
                if (rockRidgeReader.ActiveVariant != Iso9660Variant.RockRidge)
                {
                    return metadataByPath;
                }

                foreach (var fi in rockRidgeReader.Root.GetFiles("*.*", SearchOption.AllDirectories))
                {
                    string? fullName = null;
                    try
                    {
                        fullName = fi.FullName;
                        if (metadataByPath.TryGetValue(fullName, out var metadata))
                        {
                            ApplyUnixMetadata(rockRidgeReader, fullName, metadata);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e, "Could not collect RockRidge metadata for {0}", fullName ?? "an unnamed entry");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Could not read RockRidge metadata from an ISO with active variant {0}", cd.ActiveVariant);
            }

            return metadataByPath;
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
