using DiscUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public static class DiscCommon
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

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
                        var newFileEntry = await FileEntry.FromStreamAsync($"{volume.Identity}\\{file}", fileStream, parent);
                        var entries = Context.ExtractFileAsync(newFileEntry, options, governor);
                        await foreach (var entry in entries)
                        {
                            yield return entry;
                        }
                    }
                }
            }
        }


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
                        var entries = Context.ExtractFile(newFileEntry, options, governor);
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
