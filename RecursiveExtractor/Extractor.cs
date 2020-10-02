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
        /// Deprecated. Use Extract.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="opts"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use Extract instead.", false)]
        public IEnumerable<FileEntry> ExtractFile(string filename, ExtractorOptions? opts = null) => Extract(filename, opts);

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
                yield break;
            }
            using var fs = new FileStream(filename, FileMode.Open);
            foreach (var entry in Extract(filename, fs, opts))
            {
                yield return entry;
            }
        }

        /// <summary>
        /// Deprecated. Use ExtractAsync.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="opts"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use ExtractAsync instead.", false)]
        public async IAsyncEnumerable<FileEntry> ExtractFileAsync(string filename, ExtractorOptions? opts = null)
        {
            await foreach(var entry in ExtractAsync(filename, opts))
            {
                yield return entry;
            }
        }

        /// <summary>
        /// Extracts files from the `filename` given asynchronously.
        /// </summary>
        /// <param name="filename">The path to the file to extract.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <returns>The FileEntries found.</returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(string filename, ExtractorOptions? opts = null)
        {
            if (!File.Exists(filename))
            {
                Logger.Warn("ExtractFile called, but {0} does not exist.", filename);
                yield break;
            }
            using var fs = new FileStream(filename, FileMode.Open);
            await foreach (var entry in ExtractAsync(filename, fs, opts))
            {
                yield return entry;
            }
        }

        /// <summary>
        /// Deprecated. Use ExtractAsync.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="stream"></param>
        /// <param name="opts"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use ExtractAsync instead.", false)]
        public async IAsyncEnumerable<FileEntry> ExtractStreamAsync(string filename, Stream stream, ExtractorOptions? opts = null)
        {
            await foreach(var entry in ExtractAsync(filename, stream, opts))
            {
                yield return entry;
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
            var options = opts ?? new ExtractorOptions();
            var governor = new ResourceGovernor(options);
            FileEntry? fileEntry = null;
            try
            {
                var file = Path.GetFileName(filename);
                fileEntry = new FileEntry(file, stream);
                governor.ResetResourceGovernor(stream);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to extract file {0}", filename);
            }

            if (fileEntry != null)
            {
                await foreach (var result in ExtractAsync(fileEntry, opts, governor))
                {
                    governor.GovernorStopwatch.Stop();
                    yield return result;
                    governor.GovernorStopwatch.Start();
                }
            }
            governor.GovernorStopwatch.Stop();
        }

        /// <summary>
        /// Deprecated. See Extract.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="stream"></param>
        /// <param name="opts"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use Extract instead.", false)]

        public IEnumerable<FileEntry> ExtractStream(string filename, Stream stream, ExtractorOptions? opts = null) => Extract(filename, stream, opts);

        /// <summary>
        /// Extract the Stream given.
        /// </summary>
        /// <param name="filename">The filename to use for the stream.</param>
        /// <param name="stream">The Stream to extract from</param>
        /// <param name="opts">The Extractor Options to use.</param>
        /// <returns></returns>
        public IEnumerable<FileEntry> Extract(string filename, Stream stream, ExtractorOptions? opts = null)
        {
            var options = opts ?? new ExtractorOptions();
            var governor = new ResourceGovernor(options);
            governor.ResetResourceGovernor(stream);
            FileEntry? fileEntry = null;
            try
            {
                fileEntry = new FileEntry(Path.GetFileName(filename), stream);
                governor.ResetResourceGovernor(stream);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to extract file {0}", filename);
            }

            if (fileEntry != null)
            {
                foreach (var result in Extract(fileEntry, opts, governor))
                {
                    governor.GovernorStopwatch.Stop();
                    yield return result;
                    governor.GovernorStopwatch.Start();
                }
            }
            governor.GovernorStopwatch.Stop();
        }

        /// <summary>
        /// Deprecated. See Extract.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="archiveBytes"></param>
        /// <param name="opts"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use Extract instead.", false)]
        public IEnumerable<FileEntry> ExtractFile(string filename, byte[] archiveBytes, ExtractorOptions? opts = null) => Extract(filename, archiveBytes, opts);

        /// <summary>
        /// Extract from the provided bytes.
        /// </summary>
        /// <param name="filename">The filename to use for the root.</param>
        /// <param name="archiveBytes">The bytes to extract.</param>
        /// <param name="opts">The Extractor options.</param>
        /// <returns></returns>
        public IEnumerable<FileEntry> Extract(string filename, byte[] archiveBytes, ExtractorOptions? opts = null)
        {
            using var ms = new MemoryStream(archiveBytes);
            return Extract(new FileEntry(Path.GetFileName(filename), ms), opts);
        }

        /// <summary>
        /// Depreacted. Use ExtractAsync.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="archiveBytes"></param>
        /// <param name="opts"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use ExtractAsync instead.", false)]
        public async IAsyncEnumerable<FileEntry> ExtractFileAsync(string filename, byte[] archiveBytes, ExtractorOptions? opts = null)
        {
            await foreach(var entry in ExtractAsync(filename, archiveBytes, opts))
            {
                yield return entry;
            }
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
            using var ms = new MemoryStream(archiveBytes);
            await foreach (var entry in ExtractAsync(new FileEntry(Path.GetFileName(filename), ms), opts))
            {
                yield return entry;
            };
        }

        internal const string DEBUG_STRING = "Failed parsing archive of type {0} {1}:{2} ({3})";

        internal const string IS_QUINE_STRING = "Detected Quine {0} in {1}. Aborting Extraction.";

        /// <summary>
        ///     Logger for interesting events.
        /// </summary>
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Deprecated. Use ExtractAsync.
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <param name="opts"></param>
        /// <param name="governor"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use ExtractAsync instead.", false)]
        public async IAsyncEnumerable<FileEntry> ExtractFileAsync(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null)
        {
            await foreach(var entry in ExtractAsync(fileEntry, opts, governor))
            {
                yield return entry;
            }
        }

        /// <summary>
        /// Extract asynchronously from a FileEntry.
        /// </summary>
        /// <param name="fileEntry">The FileEntry containing the Conteant stream to parse.</param>
        /// <param name="opts">The ExtractorOptions to use</param>
        /// <param name="governor">The Resource governor to use (or null to create a new one).</param>
        /// <returns>The FileEntries found.</returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null)
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
            var type = MiniMagic.DetectFileType(fileEntry);
            if (type == ArchiveFileType.UNKNOWN || !Extractors.ContainsKey(type))
            {
                yield return fileEntry;
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

        private bool FileNamePasses(string fileName, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null)
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
        /// <param name="printNames">If we should print the filename when when writing it out to disc.</param>
        public ExtractionStatusCode ExtractToDirectory(string outputDirectory, string filename, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            var fs = new FileStream(filename, FileMode.Open);
            return ExtractToDirectory(outputDirectory, filename, fs, opts, acceptFilters, denyFilters, printNames);
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
        /// <param name="printNames">If we should print the filename when when writing it out to disc.</param>
        public ExtractionStatusCode ExtractToDirectory(string outputDirectory, string filename, Stream stream, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            var file = Path.GetFileName(filename);
            var fileEntry = new FileEntry(Path.GetFileName(file), stream);
            return ExtractToDirectory(outputDirectory, fileEntry, opts, acceptFilters, denyFilters, printNames);
        }

        /// <summary>
        /// Extract the given FileEntry to the given Directory.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when when writing it out to disc.</param>
        public ExtractionStatusCode ExtractToDirectory(string outputDirectory, FileEntry fileEntry, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        { 
            foreach (var entry in Extract(fileEntry, opts))
            {
                if (FileNamePasses(entry.FullPath, acceptFilters, denyFilters))
                {
                    var targetPath = Path.Combine(outputDirectory, entry.FullPath);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        
                        using var fs = new FileStream(targetPath, FileMode.Create);
                        entry.Content.CopyTo(fs);
                        if (printNames)
                        {
                            Console.WriteLine("Extracted {0}.", entry.FullPath);
                        }
                        Logger.Trace("Extracted {0}", entry.FullPath);
                    }
                    catch (Exception e)
                    {
                        Logger.Fatal(e, "Failed to create file at {0}.", targetPath);
                    }
                }
            }
            return ExtractionStatusCode.OKAY;
        }

        /// <summary>
        /// Extract the given file to the given Directory.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when when writing it out to disc.</param>
        public async Task<ExtractionStatusCode> ExtractToDirectoryAsync(string outputDirectory, string filename, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            var fs = new FileStream(filename, FileMode.Open);
            return await ExtractToDirectoryAsync(outputDirectory, filename, fs, opts, acceptFilters, denyFilters, printNames);
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
        /// <param name="printNames">If we should print the filename when when writing it out to disc.</param>
        public async Task<ExtractionStatusCode> ExtractToDirectoryAsync(string outputDirectory, string filename, Stream stream, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            var file = Path.GetFileName(filename);
            var fileEntry = new FileEntry(Path.GetFileName(file), stream);
            return await ExtractToDirectoryAsync(outputDirectory, fileEntry, opts, acceptFilters, denyFilters, printNames);
        }

        /// <summary>
        /// Extract the given FileEntry to the given Directory asynchronously.
        /// </summary>
        /// <param name="outputDirectory">The directory to extract under. (Will be created if it does not exist).</param>
        /// <param name="filename">The filename to call the stream.</param>
        /// <param name="opts">The ExtractorOptions to use.</param>
        /// <param name="acceptFilters">An optional list of regexes, when set each entry's FullName must match at least one.</param>
        /// <param name="denyFilters">An optional list of regexes, when set each entry's FullName must match none.</param>
        /// <param name="printNames">If we should print the filename when when writing it out to disc.</param>
        public async Task<ExtractionStatusCode> ExtractToDirectoryAsync(string outputDirectory, FileEntry fileEntry, ExtractorOptions? opts = null, IEnumerable<Regex>? acceptFilters = null, IEnumerable<Regex>? denyFilters = null, bool printNames = false)
        {
            await foreach (var entry in ExtractAsync(fileEntry, opts))
            {
                if (FileNamePasses(entry.FullPath, acceptFilters, denyFilters))
                {
                    var targetPath = Path.Combine(outputDirectory, entry.FullPath);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        using var fs = new FileStream(targetPath, FileMode.Create);
                        await entry.Content.CopyToAsync(fs);
                        if (printNames)
                        {
                            Console.WriteLine("Extracted {0}.", entry.FullPath);
                        }
                        Logger.Trace("Extracted {0}", entry.FullPath);
                    }
                    catch (Exception e)
                    {
                        Logger.Fatal(e, "Failed to create file at {0}.", targetPath);
                    }
                }
            }
            return ExtractionStatusCode.OKAY;
        }

        /// <summary>
        /// Deprecated. Use Extract.
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <param name="opts"></param>
        /// <param name="governor"></param>
        /// <returns></returns>
        [ObsoleteAttribute("This method is obsolete. Use Extract instead.", false)]
        public IEnumerable<FileEntry> ExtractFile(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null) => Extract(fileEntry, opts, governor);

        /// <summary>
        /// Extract from a FileEntry.
        /// </summary>
        /// <param name="fileEntry">The FileEntry containing the Conteant stream to parse.</param>
        /// <param name="opts">The ExtractorOptions to use</param>
        /// <param name="governor">The Resource governor to use (or null to create a new one).</param>
        /// <returns>The FileEntries found.</returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions? opts = null, ResourceGovernor? governor = null)
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
                    result = new[]
                    {
                        fileEntry
                    };
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