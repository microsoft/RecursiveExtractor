// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiscUtils.Btrfs;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Ntfs;
using DiscUtils.Setup;
using DiscUtils.Xfs;
using Microsoft.CST.RecursiveExtractor.Extractors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.CST.RecursiveExtractor
{
    public class Extractor
    {
        public Extractor()
        {
            SetupHelper.RegisterAssembly(typeof(BtrfsFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(ExtFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(FatFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(HfsPlusFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(NtfsFileSystem).Assembly);
            SetupHelper.RegisterAssembly(typeof(XfsFileSystem).Assembly);
            SetDefaultExtractors();
        }

        public bool EnableTiming { get; }
        internal Dictionary<ArchiveFileType, AsyncExtractorInterface> Extractors { get; } = new Dictionary<ArchiveFileType, AsyncExtractorInterface>();

        public void SetDefaultExtractors()
        {
            SetExtractor(ArchiveFileType.BZIP2, new BZip2Extractor(this));
            SetExtractor(ArchiveFileType.DEB, new DebExtractor(this));
            SetExtractor(ArchiveFileType.AR, new GnuArExtractor(this));
            SetExtractor(ArchiveFileType.GZIP, new GzipExtractor(this));
            SetExtractor(ArchiveFileType.ISO_9660, new IsoExtractor(this));
            SetExtractor(ArchiveFileType.RAR, new RarExtractor(this));
            SetExtractor(ArchiveFileType.P7ZIP, new SevenZipExtractor(this));
            SetExtractor(ArchiveFileType.TAR, new TarExtractor(this));
            SetExtractor(ArchiveFileType.VHD, new VhdExtractor(this));
            SetExtractor(ArchiveFileType.VHDX, new VhdxExtractor(this));
            SetExtractor(ArchiveFileType.VMDK, new VmdkExtractor(this));
            SetExtractor(ArchiveFileType.XZ, new XzExtractor(this));
            SetExtractor(ArchiveFileType.ZIP, new ZipExtractor(this));
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetExtractor(ArchiveFileType.WIM, new WimExtractor(this));
            }
        }

        public void SetExtractor(ArchiveFileType targetType, AsyncExtractorInterface implementation)
        {
            Extractors[targetType] = implementation;
        }

        public void ClearExtractors()
        {
            Extractors.Clear();
        }

        /// <summary>
        ///     Check if the two files are identical (i.e. Extraction is a quine)
        /// </summary>
        /// <param name="fileEntry1"> </param>
        /// <param name="fileEntry2"> </param>
        /// <returns> </returns>
        public static bool AreIdentical(FileEntry fileEntry1, FileEntry fileEntry2)
        {
            var stream1 = fileEntry1.Content;
            var stream2 = fileEntry2.Content;
            lock (stream1)
            {
                lock (stream2)
                {
                    if (stream1.CanRead && stream2.CanRead && stream1.Length == stream2.Length && fileEntry1.Name == fileEntry2.Name)
                    {
                        var bufferSize = 1024;
                        var buffer1 = new byte[bufferSize];
                        var buffer2 = new byte[bufferSize];

                        var position1 = fileEntry1.Content.Position;
                        var position2 = fileEntry2.Content.Position;
                        stream1.Position = 0;
                        stream2.Position = 0;
                        var bytesRemaining = stream2.Length;
                        while (bytesRemaining > 0)
                        {
                            stream1.Read(buffer1, 0, bufferSize);
                            stream2.Read(buffer2, 0, bufferSize);
                            if (!buffer1.SequenceEqual(buffer2))
                            {
                                stream1.Position = position1;
                                stream2.Position = position2;
                                return false;
                            }
                            bytesRemaining = stream2.Length - stream2.Position;
                        }
                        stream1.Position = position1;
                        stream2.Position = position2;
                        return true;
                    }

                    return false;
                }
            }
        }

        /// <summary>
        ///     Check if the fileEntry is a quine
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public static bool IsQuine(FileEntry fileEntry)
        {
            var next = fileEntry.Parent;
            var current = fileEntry;

            while (next != null)
            {
                if (AreIdentical(current, next))
                {
                    return true;
                }
                current = next;
                next = next.Parent;
            }

            return false;
        }

        /// <summary>
        ///     Extracts files from the file 'filename'.
        /// </summary>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> ExtractFile(string filename, ExtractorOptions? opts = null)
        {
            if (!File.Exists(filename))
            {
                Logger.Warn("ExtractFile called, but {0} does not exist.", filename);
                yield break;
            }
            using var fs = new FileStream(filename, FileMode.Open);
            foreach (var entry in ExtractStream(filename, fs, opts))
            {
                yield return entry;
            }
        }

        /// <summary>
        /// Extract a stream into FileEntries
        /// </summary>
        /// <param name="filename">The filename (with parent path) to call this root file.</param>
        /// <param name="stream">The Stream to parse.</param>
        /// <param name="parallel">Should we operate in parallel?</param>
        /// <returns></returns>
        public async IAsyncEnumerable<FileEntry> ExtractStreamAsync(string filename, Stream stream, ExtractorOptions? opts = null)
        {
            var options = opts ?? new ExtractorOptions();
            var governor = new ResourceGovernor(options);
            FileEntry? fileEntry = null;
            try
            {
                var file = Path.GetFileName(filename);
                var directory = Path.GetDirectoryName(filename);
                if (file == directory)
                {
                    directory = string.Empty;
                }
                // We give it a parent so we can give it a shortname. This is useful for Quine detection later.
                fileEntry = new FileEntry(file, stream, new FileEntry(directory, new MemoryStream()));
                governor.ResetResourceGovernor(stream);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to extract file {0}", filename);
            }

            if (fileEntry != null)
            {
                await foreach (var result in ExtractFileAsync(fileEntry, opts, governor))
                {
                    governor.GovernorStopwatch.Stop();
                    yield return result;
                    governor.GovernorStopwatch.Start();
                }
            }
            governor.GovernorStopwatch.Stop();
        }

        /// <summary>
        /// Extract a stream into FileEntries
        /// </summary>
        /// <param name="filename">The filename (with parent path) to call this root file.</param>
        /// <param name="stream">The Stream to parse.</param>
        /// <param name="parallel">Should we operate in parallel?</param>
        /// <returns></returns>
        public IEnumerable<FileEntry> ExtractStream(string filename, Stream stream, ExtractorOptions? opts = null)
        {
            var options = opts ?? new ExtractorOptions();
            var governor = new ResourceGovernor(options);
            governor.ResetResourceGovernor(stream);
            FileEntry? fileEntry = null;
            try
            {
                var file = Path.GetFileName(filename);
                var directory = Path.GetDirectoryName(filename);
                if (file == directory)
                {
                    directory = string.Empty;
                }
                // We give it a parent so we can give it a shortname. This is useful for Quine detection later.
                fileEntry = new FileEntry(file, stream, new FileEntry(directory, new MemoryStream()));
                governor.ResetResourceGovernor(stream);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to extract file {0}", filename);
            }

            if (fileEntry != null)
            {
                foreach (var result in ExtractFile(fileEntry, opts, governor))
                {
                    governor.GovernorStopwatch.Stop();
                    yield return result;
                    governor.GovernorStopwatch.Start();
                }
            }
            governor.GovernorStopwatch.Stop();
        }

        /// <summary>
        ///     Extracts files from the file, identified by 'filename', but with contents passed through
        ///     'archiveBytes'. Note that 'filename' does not have to exist; it will only be used to identify
        ///     files extracted.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> ExtractFile(string filename, byte[] archiveBytes, ExtractorOptions? opts = null)
        {
            using var ms = new MemoryStream(archiveBytes);
            return ExtractFile(new FileEntry(filename, ms), opts);
        }

        /// <summary>
        ///     Internal buffer size for extraction
        /// </summary>
        private const int BUFFER_SIZE = 32768;

        internal const string DEBUG_STRING = "Failed parsing archive of type {0} {1}:{2} ({3})";

        internal const string IS_QUINE_STRING = "Detected Quine {0} in {1}. Aborting Extraction.";

        /// <summary>
        ///     Logger for interesting events.
        /// </summary>
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Extracts files from the given FileEntry, using the appropriate extractors, recursively.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public async IAsyncEnumerable<FileEntry> ExtractFileAsync(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null)
        {
            var options = opts ?? new ExtractorOptions();
            var Governor = governor ?? new ResourceGovernor(options);
            Logger.Trace("ExtractFile({0})", fileEntry.FullPath);
            Governor.CurrentOperationProcessedBytesLeft -= fileEntry.Content.Length;
            Governor.CheckResourceGovernor();
            var useRaw = false;
            var type = MiniMagic.DetectFileType(fileEntry);
            if (type == ArchiveFileType.UNKNOWN || !Extractors.ContainsKey(type))
            {
                useRaw = true;
                var fei = new FileEntryInfo(fileEntry.Name, fileEntry.FullPath, fileEntry.Content.Length);
                if (options.Filter(fei))
                {
                    yield return await FileEntry.FromStreamAsync(fileEntry.Name, fileEntry.Content, fileEntry.Parent);
                }
            }
            else
            {
                Governor.CurrentOperationProcessedBytesLeft += fileEntry.Content.Length;
                await foreach (var result in Extractors[type].ExtractAsync(fileEntry, options, Governor))
                {
                    yield return result;
                }
            }
        }


        /// <summary>
        ///     Extracts files from the given FileEntry, using the appropriate extractors, recursively.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public IEnumerable<FileEntry> ExtractFile(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null)
        {
            var options = opts ?? new ExtractorOptions();
            var Governor = governor;
            if (Governor == null)
            {
                Governor = new ResourceGovernor(options);
                Governor.ResetResourceGovernor(fileEntry.Content);
            }
            Logger.Trace("ExtractFile({0})", fileEntry.FullPath);
            Governor.CurrentOperationProcessedBytesLeft -= fileEntry.Content.Length;
            Governor.CheckResourceGovernor();
            IEnumerable<FileEntry> result = Array.Empty<FileEntry>();
            var useRaw = false;

            try
            {
                var type = MiniMagic.DetectFileType(fileEntry);
                if (type == ArchiveFileType.UNKNOWN || !Extractors.ContainsKey(type))
                {
                    useRaw = true;
                    var fei = new FileEntryInfo(fileEntry.Name, fileEntry.FullPath, fileEntry.Content.Length);
                    if (options.Filter(fei))
                    {
                        result = new[]
                        {
                            fileEntry
                        };
                    }
                }
                else
                {
                    result = Extractors[type].Extract(fileEntry, options, Governor);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error extracting {0}: {1}", fileEntry.FullPath, ex.Message);
                useRaw = true;

                result = new[] {
                    fileEntry
                };
            }

            // After we are done with an archive subtract its bytes. Contents have been counted now separately
            if (!useRaw)
            {
                Governor.CurrentOperationProcessedBytesLeft += fileEntry.Content.Length;
            }

            return result;
        }
    }
}