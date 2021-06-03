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
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor
{
    public class Extractor
    {
        /// <summary>
        /// The main Extractor class.
        /// </summary>
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

        internal Dictionary<ArchiveFileType, AsyncExtractorInterface> Extractors { get; } = new Dictionary<ArchiveFileType, AsyncExtractorInterface>();

        /// <summary>
        /// Set up the Default Extractors compatible with this platform.
        /// </summary>
        public void SetDefaultExtractors()
        {
            SetExtractor(ArchiveFileType.BZIP2, new BZip2Extractor(this));
            SetExtractor(ArchiveFileType.DEB, new DebExtractor(this));
            SetExtractor(ArchiveFileType.AR, new GnuArExtractor(this));
            SetExtractor(ArchiveFileType.GZIP, new GzipExtractor(this));
            SetExtractor(ArchiveFileType.ISO_9660, new IsoExtractor(this));
            SetExtractor(ArchiveFileType.RAR, new RarExtractor(this));
            SetExtractor(ArchiveFileType.RAR5, new RarExtractor(this));
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

        /// <summary>
        /// Remove any extractor (if set) for the given ArchiveFileType.
        /// </summary>
        /// <param name="targetType">The ArchiveFileType to remove the extractor for.</param>
        public void Unset(ArchiveFileType targetType)
        {
            if (Extractors.ContainsKey(targetType))
            {
                Extractors.Remove(targetType);
            }
        }

        /// <summary>
        /// Set a new Extractor for the given ArchiveFileType.
        /// </summary>
        /// <param name="targetType">The ArchiveFileType to assign this extractor to.</param>
        /// <param name="implementation">The ExtractorImplementation.</param>
        public void SetExtractor(ArchiveFileType targetType, AsyncExtractorInterface implementation)
        {
            Extractors[targetType] = implementation;
        }

        /// <summary>
        /// Remove all assigned extractors.
        /// </summary>
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
                        const int bufferSize = 1024;
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
        /// Extracts files from the `filename` given.
        /// </summary>
        /// <param name="filename">The path to the file to extract.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <returns>The FileEntries found.</returns>
        public IEnumerable<FileEntry> Extract(string filename, ExtractorOptions? opts = null)
        {
            if (!File.Exists(filename))
            {
                Logger.Warn("ExtractFile called, but {0} does not exist.", filename);
                return Array.Empty<FileEntry>();
            }
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fe = new FileEntry(filename, fs, null, false, createTime: File.GetCreationTimeUtc(filename), modifyTime: File.GetLastWriteTimeUtc(filename), accessTime: File.GetLastAccessTimeUtc(filename));
            return Extract(fe, opts);
        }

        /// <summary>
        /// Extracts files from the `filename` given asynchronously.
        /// </summary>
        /// <param name="filename">The path to the file to extract.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <returns>The FileEntries found.</returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(string filename, ExtractorOptions? opts = null)
        {
            opts ??= new ExtractorOptions();
            if (!File.Exists(filename))
            {
                Logger.Warn("ExtractFile called, but {0} does not exist.", filename);
                yield break;
            }
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fe = new FileEntry(filename, fs, null, false, createTime: File.GetCreationTimeUtc(filename), modifyTime: File.GetLastWriteTimeUtc(filename), accessTime: File.GetLastAccessTimeUtc(filename));
            await foreach (var entry in ExtractAsync(fe, opts))
            {
                if (opts.FileNamePasses(entry.FullPath))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Extract from a Stream asynchronously.
        /// </summary>
        /// <param name="filename">The filename to call the Stream.</param>
        /// <param name="stream">The Stream to parse.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <returns>The FileEntries found.</returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(string filename, Stream stream, ExtractorOptions? opts = null)
        {
            opts ??= new ExtractorOptions();
            var governor = new ResourceGovernor(opts);
            FileEntry? fileEntry = null;
            var file = Path.GetFileName(filename);
            fileEntry = new FileEntry(file, stream, memoryStreamCutoff: opts.MemoryStreamCutoff);
            governor.ResetResourceGovernor(stream);
            

            if (fileEntry != null)
            {
                await foreach (var result in ExtractAsync(fileEntry, opts, governor, true))
                {
                    governor.GovernorStopwatch.Stop();
                    if (opts.FileNamePasses(result.FullPath))
                    {
                        yield return result;
                    }
                    governor.GovernorStopwatch.Start();
                }
            }
            governor.GovernorStopwatch.Stop();
        }

        /// <summary>
        /// Extract the Stream given.
        /// </summary>
        /// <param name="filename">The filename to use for the stream.</param>
        /// <param name="stream">The Stream to extract from</param>
        /// <param name="opts">The Extractor Options to use.</param>
        /// <returns></returns>
        public IEnumerable<FileEntry> Extract(string filename, Stream stream, ExtractorOptions? opts = null)
        {
            opts ??= new ExtractorOptions();
            var fileEntry = new FileEntry(filename, stream, memoryStreamCutoff: opts.MemoryStreamCutoff);
            return Extract(fileEntry, opts);
        }

        /// <summary>
        /// Extract from the provided bytes.
        /// </summary>
        /// <param name="filename">The filename to use for the root.</param>
        /// <param name="archiveBytes">The bytes to extract.</param>
        /// <param name="opts">The Extractor options.</param>
        /// <returns></returns>
        public IEnumerable<FileEntry> Extract(string filename, byte[] archiveBytes, ExtractorOptions? opts = null)
        {
            opts ??= new ExtractorOptions();
            using var ms = new MemoryStream(archiveBytes);
            return Extract(new FileEntry(filename, ms, memoryStreamCutoff: opts.MemoryStreamCutoff), opts);
        }

        /// <summary>
        /// Extract from the provided bytes async.
        /// </summary>
        /// <param name="filename">The filename to use for the root.</param>
        /// <param name="archiveBytes">The bytes to extract.</param>
        /// <param name="opts">The Extractor options.</param>
        /// <returns>The FileEntrys found.</returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(string filename, byte[] archiveBytes, ExtractorOptions? opts = null)
        {
            opts ??= new ExtractorOptions();
            using var ms = new MemoryStream(archiveBytes);
            await foreach (var entry in ExtractAsync(new FileEntry(Path.GetFileName(filename), ms, memoryStreamCutoff: opts.MemoryStreamCutoff), opts))
            {
                if (opts.FileNamePasses(entry.FullPath))
                {
                    yield return entry;
                }
            }
        }

        internal const string DEBUG_STRING = "Failed parsing archive of type {0} {1}:{2} ({3})";

        internal const string IS_QUINE_STRING = "Detected Quine {0} in {1}. Aborting Extraction.";

        /// <summary>
        ///     Logger for interesting events.
        /// </summary>
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Extract asynchronously from a FileEntry.
        /// </summary>
        /// <param name="fileEntry">The FileEntry containing the Conteant stream to parse.</param>
        /// <param name="opts">The ExtractorOptions to use</param>
        /// <param name="governor">The Resource governor to use (or null to create a new one).</param>
        /// <returns>The FileEntries found.</returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null, bool topLevel = true)
        {
            var options = opts ?? new ExtractorOptions();

            var Governor = governor ?? new ResourceGovernor(options);
            if (governor is null)
            {
                Governor.ResetResourceGovernor(fileEntry.Content);
            }
            Logger.Trace("ExtractFile({0})", fileEntry.FullPath);

            Governor.CurrentOperationProcessedBytesLeft -= fileEntry.Content.Length;
            Governor.CheckResourceGovernor();
            if (IsQuine(fileEntry))
            {
                Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                throw new OverflowException();
            }
            if (topLevel || (!topLevel && options.Recurse))
            {
                var type = MiniMagic.DetectFileType(fileEntry);

                if (((opts?.RawExtensions?.Any(x => Path.GetExtension(fileEntry.FullPath).Equals(x)) ?? false) || type == ArchiveFileType.UNKNOWN || !Extractors.ContainsKey(type)))
                {
                    if (options.FileNamePasses(fileEntry.FullPath))
                    {
                        yield return fileEntry;
                    }
                }
                else
                {
                    await foreach (var result in Extractors[type].ExtractAsync(fileEntry, options, Governor, false))
                    {
                        if (options.FileNamePasses(result.FullPath))
                        {
                            yield return result;
                        }
                    }
                }
            }
            else if (options.FileNamePasses(fileEntry.FullPath))
            {
                yield return fileEntry;
            }
        }

        private static bool FileNamePasses(string fileName, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null)
        {
            foreach (var allowRegex in acceptFilters ?? Array.Empty<Regex>())
            {
                if (!allowRegex.IsMatch(fileName))
                {
                    return false;
                }
            }
            foreach (var denyRegex in denyFilters ?? Array.Empty<Regex>())
            {
                if (denyRegex.IsMatch(fileName))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Extract the given file to the given Directory.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when writing it out to disc.</param>
        public ExtractionStatusCode ExtractToDirectory(string outputDirectory, string filename, ExtractorOptions? opts = null, bool printNames = false)
        {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ExtractToDirectory(outputDirectory, filename, fs, opts, printNames);
        }

        /// <summary>
        /// Extract the given Stream to the given Directory.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="stream">The Stream to extract.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when writing it out to disc.</param>
        public ExtractionStatusCode ExtractToDirectory(string outputDirectory, string filename, Stream stream, ExtractorOptions? opts = null, bool printNames = false)
        {
            opts ??= new ExtractorOptions();
            var file = Path.GetFileName(filename);
            var fileEntry = new FileEntry(Path.GetFileName(file), stream, memoryStreamCutoff: opts.MemoryStreamCutoff);
            return ExtractToDirectory(outputDirectory, fileEntry, opts, printNames);
        }

        /// <summary>
        /// Extract the given FileEntry to the given Directory.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when writing it out to disc.</param>
        public ExtractionStatusCode ExtractToDirectory(string outputDirectory, FileEntry fileEntry, ExtractorOptions? opts = null, bool printNames = false)
        {
            opts ??= new ExtractorOptions();
            if (opts.Parallel)
            {

            }
            else
            {
                var exitCode = ExtractionStatusCode.Ok;
                var cts = new CancellationTokenSource();
                try
                {
                    Parallel.ForEach(Extract(fileEntry, opts), new ParallelOptions() { CancellationToken = cts.Token }, entry =>
                    {
                        var targetPath = Path.Combine(outputDirectory, entry.FullPath);
                        if (Path.GetDirectoryName(targetPath) is string directoryPath && targetPath is string targetPathNotNull)
                        {
                            try
                            {
                                Directory.CreateDirectory(directoryPath);

                                using var fs = new FileStream(targetPathNotNull, FileMode.Create);
                                entry.Content.CopyTo(fs);
                                if (printNames)
                                {
                                    Console.WriteLine("Extracted {0}.", entry.FullPath);
                                }
                                Logger.Trace("Extracted {0}", entry.FullPath);
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, "Failed to create file at {0}.", targetPathNotNull);
                                cts.Cancel();
                            }
                        }
                        else
                        {
                            Logger.Error("Failed to create directory.");
                            cts.Cancel();
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    exitCode = ExtractionStatusCode.Failure;
                }
                return exitCode;
            }
            
            return ExtractionStatusCode.Ok;
        }

        /// <summary>
        /// Extract the given file to the given Directory.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when writing it out to disc.</param>
        public async Task<ExtractionStatusCode> ExtractToDirectoryAsync(string outputDirectory, string filename, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await ExtractToDirectoryAsync(outputDirectory, filename, fs, opts, acceptFilters, denyFilters, printNames).ConfigureAwait(false);
        }

        /// <summary>
        /// Extract the given Stream to the given Directory asynchronously.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="stream">The Stream to extract.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when writing it out to disc.</param>
        public async Task<ExtractionStatusCode> ExtractToDirectoryAsync(string outputDirectory, string filename, Stream stream, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            opts ??= new ExtractorOptions();
            var file = Path.GetFileName(filename);
            var fileEntry = new FileEntry(Path.GetFileName(file), stream, memoryStreamCutoff: opts.MemoryStreamCutoff);
            return await ExtractToDirectoryAsync(outputDirectory, fileEntry, opts, acceptFilters, denyFilters, printNames).ConfigureAwait(false);
        }

        /// <summary>
        /// Extract the given FileEntry to the given Directory asynchronously.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when writing it out to disc.</param>
        public async Task<ExtractionStatusCode> ExtractToDirectoryAsync(string outputDirectory, FileEntry fileEntry, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            await foreach (var entry in ExtractAsync(fileEntry, opts))
            {
                if (FileNamePasses(entry.FullPath, acceptFilters, denyFilters))
                {
                    var targetPath = Path.Combine(outputDirectory, entry.FullPath);
                    if (Path.GetDirectoryName(targetPath) is string directoryPath && targetPath is string targetPathNotNull)
                    {
                        try
                        {
                            Directory.CreateDirectory(directoryPath);
                            using var fs = new FileStream(targetPathNotNull, FileMode.Create);
                            await entry.Content.CopyToAsync(fs).ConfigureAwait(false);
                            if (printNames)
                            {
                                Console.WriteLine("Extracted {0}.", entry.FullPath);
                            }
                            Logger.Trace("Extracted {0}", entry.FullPath);
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Failed to create file at {0}.", targetPathNotNull);
                        }
                    }
                    else
                    {
                        Logger.Error("Failed to create directory.");
                    }
                }
            }
            return ExtractionStatusCode.Ok;
        }

        /// <summary>
        /// Extract from a FileEntry.
        /// </summary>
        /// <param name="fileEntry">The FileEntry containing the Conteant stream to parse.</param>
        /// <param name="opts">The ExtractorOptions to use</param>
        /// <param name="governor">The Resource governor to use (or null to create a new one).</param>
        /// <returns>The FileEntries found.</returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null, bool topLevel = true)
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
            if (IsQuine(fileEntry))
            {
                Logger.Info(IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                throw new OverflowException();
            }
            List<FileEntry> result = new List<FileEntry>();
            var useRaw = false;

            if (topLevel || options.Recurse)
            {
                var type = MiniMagic.DetectFileType(fileEntry);

                if ((opts?.RawExtensions?.Any(x => Path.GetExtension(fileEntry.FullPath).Equals(x)) ?? false) || type == ArchiveFileType.UNKNOWN || !Extractors.ContainsKey(type))
                {
                    if (options.FileNamePasses(fileEntry.FullPath))
                    {
                        result.Add(fileEntry);
                    }
                }
                else
                {
                    foreach (var extractedResult in Extractors[type].Extract(fileEntry, options, Governor, false))
                    {
                        if (options.FileNamePasses(extractedResult.FullPath))
                        {
                            result.Add(extractedResult);
                        }
                    }
                }
            }
            else if (options.FileNamePasses(fileEntry.FullPath))
            {
                result.Add(fileEntry);
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