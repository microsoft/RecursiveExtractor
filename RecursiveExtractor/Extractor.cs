// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using DiscUtils;
using DiscUtils.Btrfs;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Iso9660;
using DiscUtils.Ntfs;
using DiscUtils.Setup;
using DiscUtils.Streams;
using DiscUtils.Xfs;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        }

        public bool EnableTiming { get; }

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
        public IEnumerable<FileEntry> ExtractStream(string filename, Stream stream, ExtractorOptions? opts = null)
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

        private const string DEBUG_STRING = "Failed parsing archive of type {0} {1}:{2} ({3})";

        private readonly string IS_QUINE_STRING = "Detected Quine {0} in {1}. Aborting Extraction.";

        /// <summary>
        ///     Logger for interesting events.
        /// </summary>
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private IEnumerable<FileEntry> DumpLogicalVolume(LogicalVolumeInfo volume, string parentPath, ExtractorOptions options, ResourceGovernor governor, FileEntry? parent = null)
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
                        var fileinfos = new List<(DiscFileInfo, Stream)>();
                        long totalLength = 0;
                        foreach (var r in range)
                        {
                            try
                            {
                                var fi = fs.GetFileInfo(r);
                                totalLength += fi.Length;
                                var fei = new FileEntryInfo(fi.FullName, parentPath, fi.Length);
                                if (options.Filter(fei))
                                {
                                    fileinfos.Add((fi, fi.OpenRead()));
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Debug("Failed to get FileInfo from {0} in Volume {1} @ {2} ({3}:{4})", r, volume.Identity, parentPath, e.GetType(), e.Message);
                            }
                        }

                        governor.CheckResourceGovernor(totalLength);

                        fileinfos.AsParallel().ForAll(file =>
                        {
                            if (file.Item2 != null)
                            {
                                var newFileEntry = new FileEntry($"{volume.Identity}\\{file.Item1.FullName}", file.Item2, parent);
                                var entries = ExtractFile(newFileEntry, options, governor);
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
                            var entries = ExtractFile(newFileEntry, options, governor);
                            foreach (var entry in entries)
                            {
                                yield return entry;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Extracts a 7-Zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> Extract7ZipFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            SevenZipArchive? sevenZipArchive = null;
            try
            {
                sevenZipArchive = SevenZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (sevenZipArchive != null)
            {
                var entries = sevenZipArchive.Entries.Where(x => !x.IsDirectory && !x.IsEncrypted && x.IsComplete && options.Filter(new FileEntryInfo(x.Key, fileEntry.FullPath, x.Size))).ToList();

                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Count() > 0)
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count());
                        var selectedEntries = entries.GetRange(0, batchSize).Select(entry => (entry, entry.OpenEntryStream()));
                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.entry.Size));

                        try
                        {
                            selectedEntries.AsParallel().ForAll(entry =>
                            {
                                try
                                {
                                    var newFileEntry = new FileEntry(entry.entry.Key, entry.Item2, fileEntry);
                                    if (IsQuine(newFileEntry))
                                    {
                                        Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                        governor.CurrentOperationProcessedBytesLeft = -1;
                                    }
                                    else
                                    {
                                        files.PushRange(ExtractFile(newFileEntry, options, governor).ToArray());
                                    }
                                }
                                catch (Exception e) when (e is OverflowException)
                                {
                                    Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.entry.Key, e.GetType());
                                    throw;
                                }
                                catch (Exception e)
                                {
                                    Logger.Debug(DEBUG_STRING, ArchiveFileType.P7ZIP, fileEntry.FullPath, entry.entry.Key, e.GetType());
                                }
                            });
                        }
                        catch (Exception e) when (e is AggregateException)
                        {
                            if (e.InnerException?.GetType() == typeof(OverflowException))
                            {
                                throw e.InnerException;
                            }
                            throw;
                        }

                        governor.CheckResourceGovernor(0);
                        entries.RemoveRange(0, batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        governor.CheckResourceGovernor(entry.Size);
                        var newFileEntry = new FileEntry(entry.Key, entry.OpenEntryStream(), fileEntry);

                        if (IsQuine(newFileEntry))
                        {
                            Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
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
        ///     Extracts an BZip2 file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractBZip2File(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            BZip2Stream? bzip2Stream = null;
            try
            {
                bzip2Stream = new BZip2Stream(fileEntry.Content, SharpCompress.Compressors.CompressionMode.Decompress, false);
                governor.CheckResourceGovernor(bzip2Stream.Length);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.BZIP2, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (bzip2Stream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, bzip2Stream, fileEntry);

                if (IsQuine(newFileEntry))
                {
                    Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    bzip2Stream.Dispose();
                    throw new OverflowException();
                }

                foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                {
                    yield return extractedFile;
                }
                bzip2Stream.Dispose();
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
        ///     Extracts a .deb file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractDebFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            IEnumerable<FileEntry>? entries = null;
            try
            {
                entries = DebArchiveFile.GetFileEntries(fileEntry, options);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.DEB, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (entries != null)
            {
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Any())
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count());
                        var selectedEntries = entries.Take(batchSize);

                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.Content.Length));

                        selectedEntries.AsParallel().ForAll(entry =>
                        {
                            files.PushRange(ExtractFile(entry, options, governor).ToArray());
                        });

                        entries = entries.Skip(batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        governor.CheckResourceGovernor(entry.Content.Length);
                        foreach (var extractedFile in ExtractFile(entry, options, governor))
                        {
                            yield return extractedFile;
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
        ///     Extracts files from the given FileEntry, using the appropriate extractors, recursively.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractFile(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null)
        {
            var options = opts ?? new ExtractorOptions();
            var Governor = governor ?? new ResourceGovernor(options);
            Logger.Trace("ExtractFile({0})", fileEntry.FullPath);
            Governor.CurrentOperationProcessedBytesLeft -= fileEntry.Content.Length;
            Governor.CheckResourceGovernor();
            IEnumerable<FileEntry> result = Array.Empty<FileEntry>();
            var useRaw = false;

            try
            {
                switch (MiniMagic.DetectFileType(fileEntry))
                {
                    case ArchiveFileType.ZIP:
                        result = ExtractZipFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.RAR:
                        result = ExtractRarFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.P7ZIP:
                        result = Extract7ZipFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.DEB:
                        result = ExtractDebFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.GZIP:
                        result = ExtractGZipFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.TAR:
                        result = ExtractTarFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.XZ:
                        result = ExtractXZFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.BZIP2:
                        result = ExtractBZip2File(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.AR:
                        result = ExtractGnuArFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.ISO_9660:
                        result = ExtractIsoFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.VHDX:
                        result = ExtractVHDXFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.VHD:
                        result = ExtractVHDFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.WIM:
                        result = ExtractWimFile(fileEntry, options, Governor);
                        break;

                    case ArchiveFileType.VMDK:
                        result = ExtractVMDKFile(fileEntry, options, Governor);
                        break;

                    default:
                        useRaw = true;
                        var fei = new FileEntryInfo(fileEntry.Name, fileEntry.FullPath, fileEntry.Content.Length);
                        if (options.Filter(fei))
                        {
                            result = new[]
                            {
                            fileEntry.Passthrough ? new FileEntry(fileEntry.Name,fileEntry.Content,fileEntry.Parent) : fileEntry
                        };
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error extracting {0}: {1}", fileEntry.FullPath, ex.Message);
                useRaw = true;

                result = new[] {
                    fileEntry.Passthrough ? new FileEntry(fileEntry.Name,fileEntry.Content,fileEntry.Parent) : fileEntry
                };
            }

            // After we are done with an archive subtract its bytes. Contents have been counted now separately
            if (!useRaw)
            {
                governor.CurrentOperationProcessedBytesLeft += fileEntry.Content.Length;
            }

            return result;
        }

        /// <summary>
        ///     Extracts an archive file created with GNU ar
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        private IEnumerable<FileEntry> ExtractGnuArFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            IEnumerable<FileEntry>? fileEntries = null;
            try
            {
                fileEntries = ArFile.GetFileEntries(fileEntry, options);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.AR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (fileEntries != null)
            {
                if (options.Parallel)
                {
                    var tempStore = new ConcurrentStack<FileEntry>();
                    var selectedEntries = fileEntries.Take(options.BatchSize);
                    governor.CheckResourceGovernor(selectedEntries.Sum(x => x.Content.Length));
                    selectedEntries.AsParallel().ForAll(arEntry =>
                    {
                        tempStore.PushRange(ExtractFile(arEntry, options, governor).ToArray());
                    });

                    fileEntries = fileEntries.Skip(selectedEntries.Count());

                    while (tempStore.TryPop(out var result))
                    {
                        if (result != null)
                            yield return result;
                    }
                }
                else
                {
                    foreach (var entry in fileEntries)
                    {
                        governor.CheckResourceGovernor(entry.Content.Length);
                        foreach (var extractedFile in ExtractFile(entry, options, governor))
                        {
                            yield return extractedFile;
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
        ///     Extracts an Gzip file contained in fileEntry. Since this function is recursive, even though
        ///     Gzip only supports a single compressed file, that inner file could itself contain multiple others.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractGZipFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            GZipArchive? gzipArchive = null;
            try
            {
                gzipArchive = GZipArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.GZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (gzipArchive != null)
            {
                foreach (var entry in gzipArchive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        continue;
                    }

                    governor.CheckResourceGovernor(entry.Size);

                    var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                    if (fileEntry.Name.EndsWith(".tgz", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newFilename = newFilename[0..^4] + ".tar";
                    }

                    FileEntry? newFileEntry = null;
                    try
                    {
                        using var stream = entry.OpenEntryStream();
                        newFileEntry = new FileEntry(newFilename, stream, fileEntry);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(DEBUG_STRING, ArchiveFileType.GZIP, fileEntry.FullPath, newFilename, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
                    }
                }
                gzipArchive.Dispose();
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
        ///     Extracts an an ISO file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        private IEnumerable<FileEntry> ExtractIsoFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var cd = new CDReader(fileEntry.Content, true);
            var entries = cd.GetFiles(cd.Root.FullName, "*.*", SearchOption.AllDirectories);
            if (entries != null)
            {
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    var batchSize = Math.Min(options.BatchSize, entries.Length);
                    var selectedFileNames = entries[0..batchSize];
                    var fileInfoTuples = new List<(DiscFileInfo, Stream)>();

                    foreach (var selectedFileName in selectedFileNames)
                    {
                        try
                        {
                            var fileInfo = cd.GetFileInfo(selectedFileName);
                            var fei = new FileEntryInfo(fileInfo.FullName, fileEntry.FullPath, fileInfo.Length);
                            if (options.Filter(fei))
                            {
                                var stream = fileInfo.OpenRead();

                                fileInfoTuples.Add((fileInfo, stream));
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Failed to get FileInfo or OpenStream from {0} in ISO {1} ({2}:{3})", selectedFileName, fileEntry.FullPath, e.GetType(), e.Message);
                        }
                    }

                    governor.CheckResourceGovernor(fileInfoTuples.Sum(x => x.Item1.Length));

                    fileInfoTuples.AsParallel().ForAll(cdFile =>
                    {
                        var newFileEntry = new FileEntry(cdFile.Item1.Name, cdFile.Item2, fileEntry);
                        var entries = ExtractFile(newFileEntry, options, governor);
                        files.PushRange(entries.ToArray());
                    });

                    entries = entries[batchSize..];

                    while (files.TryPop(out var result))
                    {
                        if (result != null)
                            yield return result;
                    }
                }
                else
                {
                    foreach (var file in entries)
                    {
                        var fileInfo = cd.GetFileInfo(file);
                        governor.CheckResourceGovernor(fileInfo.Length);
                        Stream? stream = null;
                        try
                        {
                            var fei = new FileEntryInfo(fileInfo.Name, Path.Combine(fileEntry.FullPath, fileInfo.FullName), fileInfo.Length);
                            if (options.Filter(fei))
                            {
                                stream = fileInfo.OpenRead();
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.Name, fileEntry.FullPath, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var newFileEntry = new FileEntry(fileInfo.Name, stream, fileEntry);
                            var innerEntries = ExtractFile(newFileEntry, options, governor);
                            foreach (var entry in innerEntries)
                            {
                                yield return entry;
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
        ///     Extracts a RAR file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractRarFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            RarArchive? rarArchive = null;
            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
            }

            if (rarArchive != null)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory && !x.IsEncrypted && options.Filter(new FileEntryInfo(x.Key, fileEntry.FullPath, x.Size)));
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    while (entries.Any())
                    {
                        var batchSize = Math.Min(options.BatchSize, entries.Count());

                        var streams = entries.Take(batchSize).Select(entry => (entry, entry.OpenEntryStream())).ToList();

                        governor.CheckResourceGovernor(streams.Sum(x => x.Item2.Length));

                        streams.AsParallel().ForAll(streampair =>
                        {
                            try
                            {
                                var newFileEntry = new FileEntry(streampair.entry.Key, streampair.Item2, fileEntry);
                                if (IsQuine(newFileEntry))
                                {
                                    Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                    governor.CurrentOperationProcessedBytesLeft = -1;
                                }
                                else
                                {
                                    files.PushRange(ExtractFile(newFileEntry, options, governor).ToArray());
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, streampair.entry.Key, e.GetType());
                            }
                        });
                        governor.CheckResourceGovernor(0);

                        entries = entries.Skip(streams.Count);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        governor.CheckResourceGovernor(entry.Size);
                        FileEntry? newFileEntry = null;
                        try
                        {
                            newFileEntry = new FileEntry(entry.Key, entry.OpenEntryStream(), fileEntry);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                        }
                        if (newFileEntry != null)
                        {
                            if (IsQuine(newFileEntry))
                            {
                                Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                throw new OverflowException();
                            }
                            foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                            {
                                yield return extractedFile;
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
        ///     Extracts a tar file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractTarFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            TarEntry tarEntry;
            TarInputStream? tarStream = null;
            try
            {
                tarStream = new TarInputStream(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (tarStream != null)
            {
                while ((tarEntry = tarStream.GetNextEntry()) != null)
                {
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }
                    var fei = new FileEntryInfo(tarEntry.Name, fileEntry.FullPath, tarEntry.Size);
                    if (options.Filter(fei))
                    {
                        var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                        governor.CheckResourceGovernor(tarStream.Length);
                        try
                        {
                            tarStream.CopyEntryContents(fs);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Name, e.GetType());
                        }

                        var newFileEntry = new FileEntry(tarEntry.Name, fs, fileEntry, true);

                        if (IsQuine(newFileEntry))
                        {
                            Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }

                        foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
                    }
                }
                tarStream.Dispose();
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
        ///     Extracts an a VHD file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        private IEnumerable<FileEntry> ExtractVHDFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var disk = new DiscUtils.Vhd.Disk(fileEntry.Content, Ownership.None);
            LogicalVolumeInfo[]? logicalVolumes = null;

            try
            {
                var manager = new VolumeManager(disk);
                logicalVolumes = manager.GetLogicalVolumes();
            }
            catch (Exception e)
            {
                Logger.Debug("Error reading {0} disk at {1} ({2}:{3})", disk.GetType(), fileEntry.FullPath, e.GetType(), e.Message);
            }

            if (logicalVolumes != null)
            {
                foreach (var volume in logicalVolumes)
                {
                    foreach (var entry in DumpLogicalVolume(volume, fileEntry.FullPath, options, governor, fileEntry))
                    {
                        yield return entry;
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
        ///     Extracts an a VHDX file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        private IEnumerable<FileEntry> ExtractVHDXFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var disk = new DiscUtils.Vhdx.Disk(fileEntry.Content, Ownership.None);
            LogicalVolumeInfo[]? logicalVolumes = null;

            try
            {
                var manager = new VolumeManager(disk);
                logicalVolumes = manager.GetLogicalVolumes();
            }
            catch (Exception e)
            {
                Logger.Debug("Error reading {0} disk at {1} ({2}:{3})", disk.GetType(), fileEntry.FullPath, e.GetType(), e.Message);
            }

            if (logicalVolumes != null)
            {
                foreach (var volume in logicalVolumes)
                {
                    var fsInfos = FileSystemManager.DetectFileSystems(volume);

                    foreach (var entry in DumpLogicalVolume(volume, fileEntry.FullPath, options, governor, fileEntry))
                    {
                        yield return entry;
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
        ///     Extracts an a VMDK file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        private IEnumerable<FileEntry> ExtractVMDKFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var disk = new DiscUtils.Vmdk.Disk(fileEntry.Content, Ownership.None);
            LogicalVolumeInfo[]? logicalVolumes = null;

            try
            {
                var manager = new VolumeManager(disk);
                logicalVolumes = manager.GetLogicalVolumes();
            }
            catch (Exception e)
            {
                Logger.Debug("Error reading {0} disk at {1} ({2}:{3})", disk.GetType(), fileEntry.FullPath, e.GetType(), e.Message);
            }

            if (logicalVolumes != null)
            {
                foreach (var volume in logicalVolumes)
                {
                    foreach (var entry in DumpLogicalVolume(volume, fileEntry.FullPath, options, governor, fileEntry))
                    {
                        yield return entry;
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
        ///     Extracts an a Wim file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        private IEnumerable<FileEntry> ExtractWimFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
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
                                    var fei = new FileEntryInfo(info.FullName, fileEntry.FullPath, read.Length);
                                    if (options.Filter(fei))
                                    {
                                        streamsAndNames.Add((info, read));
                                    }
                                    else
                                    {
                                        read.Dispose();
                                    }
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
                                var entries = ExtractFile(newFileEntry, options, governor);
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
                                var fei = new FileEntryInfo(info.FullName, fileEntry.FullPath, stream.Length);
                                if (!options.Filter(fei))
                                {
                                    stream.Dispose();
                                    stream = null;
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Debug("Error reading {0} from WIM {1} ({2}:{3})", file, image.FriendlyName, e.GetType(), e.Message);
                            }
                            if (stream != null)
                            {
                                var newFileEntry = new FileEntry($"{image.FriendlyName}\\{file}", stream, fileEntry);
                                foreach (var entry in ExtractFile(newFileEntry, options, governor))
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
        ///     Extracts an .XZ file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractXZFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            XZStream? xzStream = null;
            try
            {
                xzStream = new XZStream(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.XZ, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (xzStream != null)
            {
                var newFilename = Path.GetFileNameWithoutExtension(fileEntry.Name);
                var newFileEntry = new FileEntry(newFilename, xzStream, fileEntry);

                // SharpCompress does not expose metadata without a full read, so we need to decompress first,
                // and then abort if the bytes exceeded the governor's capacity.

                var streamLength = xzStream.Index.Records?.Select(r => r.UncompressedSize)
                                          .Aggregate((ulong?)0, (a, b) => a + b);

                // BUG: Technically, we're casting a ulong to a long, but we don't expect 9 exabyte steams, so
                // low risk.
                if (streamLength.HasValue)
                {
                    governor.CheckResourceGovernor((long)streamLength.Value);
                }

                if (IsQuine(newFileEntry))
                {
                    Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                    throw new OverflowException();
                }

                foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                {
                    yield return extractedFile;
                }
                xzStream.Dispose();
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
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        private IEnumerable<FileEntry> ExtractZipFile(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            ZipFile? zipFile = null;
            try
            {
                zipFile = new ZipFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipFile != null)
            {
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    var zipEntries = new List<ZipEntry>();
                    foreach (ZipEntry? zipEntry in zipFile)
                    {
                        if (zipEntry is null ||
                            zipEntry.IsDirectory ||
                            zipEntry.IsCrypted ||
                            !zipEntry.CanDecompress)
                        {
                            continue;
                        }
                        var fei = new FileEntryInfo(zipEntry.Name, fileEntry.FullPath, zipEntry.Size);
                        if (options.Filter(fei))
                        {
                            zipEntries.Add(zipEntry);
                        }
                    }

                    while (zipEntries.Count > 0)
                    {
                        var batchSize = Math.Min(options.BatchSize, zipEntries.Count);
                        var selectedEntries = zipEntries.GetRange(0, batchSize);
                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.Size));
                        try
                        {
                            selectedEntries.AsParallel().ForAll(zipEntry =>
                            {
                                try
                                {
                                    var zipStream = zipFile.GetInputStream(zipEntry);
                                    var newFileEntry = new FileEntry(zipEntry.Name, zipStream, fileEntry);
                                    if (IsQuine(newFileEntry))
                                    {
                                        Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                        governor.CurrentOperationProcessedBytesLeft = -1;
                                    }
                                    else
                                    {
                                        files.PushRange(ExtractFile(newFileEntry, options, governor).ToArray());
                                    }
                                }
                                catch (Exception e) when (e is OverflowException)
                                {
                                    Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                                    throw;
                                }
                                catch (Exception e)
                                {
                                    Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                                }
                            });
                        }
                        catch (Exception e) when (e is AggregateException)
                        {
                            if (e.InnerException?.GetType() == typeof(OverflowException))
                            {
                                throw e.InnerException;
                            }
                            throw;
                        }

                        governor.CheckResourceGovernor(0);
                        zipEntries.RemoveRange(0, batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (ZipEntry? zipEntry in zipFile)
                    {
                        if (zipEntry is null ||
                            zipEntry.IsDirectory ||
                            zipEntry.IsCrypted ||
                            !zipEntry.CanDecompress)
                        {
                            continue;
                        }

                        governor.CheckResourceGovernor(zipEntry.Size);

                        using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                        try
                        {
                            var buffer = new byte[BUFFER_SIZE];
                            var zipStream = zipFile.GetInputStream(zipEntry);
                            StreamUtils.Copy(zipStream, fs, buffer);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                        }

                        var newFileEntry = new FileEntry(zipEntry.Name, fs, fileEntry);

                        if (IsQuine(newFileEntry))
                        {
                            Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }

                        foreach (var extractedFile in ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
                    }
                }
            }
        }
    }
}