using DiscUtils;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The VHD Extractor implementation
    /// </summary>
    public class VhdExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public VhdExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an a VHD file
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            LogicalVolumeInfo[]? logicalVolumes = null;

            try
            {
                using var disk = new DiscUtils.Vhd.Disk(fileEntry.Content, Ownership.None);
                var manager = new VolumeManager(disk);
                logicalVolumes = manager.GetLogicalVolumes();
            }
            catch (Exception e)
            {
                Logger.Debug("Error reading {0} disk at {1} ({2}:{3})", fileEntry.ArchiveType, fileEntry.FullPath, e.GetType(), e.Message);
            }

            if (logicalVolumes != null)
            {
                foreach (var volume in logicalVolumes)
                {
                    await foreach (var entry in DiscCommon.DumpLogicalVolumeAsync(volume, fileEntry.FullPath, options, governor, Context, fileEntry, topLevel))
                    {
                        yield return entry;
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
        ///     Extracts an a VHD file
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            LogicalVolumeInfo[]? logicalVolumes = null;

            try
            {
                using var disk = new DiscUtils.Vhd.Disk(fileEntry.Content, Ownership.None);
                var manager = new VolumeManager(disk);
                logicalVolumes = manager.GetLogicalVolumes();
            }
            catch (Exception e)
            {
                Logger.Debug("Error reading {0} disk at {1} ({2}:{3})", fileEntry.ArchiveType, fileEntry.FullPath, e.GetType(), e.Message);
            }

            if (logicalVolumes != null)
            {
                foreach (var volume in logicalVolumes)
                {
                    foreach (var entry in DiscCommon.DumpLogicalVolume(volume, fileEntry.FullPath, options, governor, Context, fileEntry, topLevel))
                    {
                        yield return entry;
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