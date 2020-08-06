using DiscUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public abstract class DiscExtractorImplementation : ExtractorImplementation
    {
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal abstract Extractor GetContext();

        internal IEnumerable<FileEntry> DumpLogicalVolume(LogicalVolumeInfo volume, string parentPath, ExtractorOptions options, ResourceGovernor governor, FileEntry? parent = null)
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
                        try
                        {
                            var fi = fs.GetFileInfo(file);
                            governor.CheckResourceGovernor(fi.Length);
                            fileStream = fi.OpenRead();
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(e, "Failed to open {0} in volume {1}", file, volume.Identity);
                        }
                        if (fileStream != null)
                        {
                            var newFileEntry = new FileEntry($"{volume.Identity}\\{file}", fileStream, parent);
                            var entries = GetContext().ExtractFile(newFileEntry, options, governor);
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