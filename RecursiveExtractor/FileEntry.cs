// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// The FileEntry Class represents a logical file. It is generally constructed from a stream and may correspond to a File on Disk, a Stream or a file extracted from another FileEntry.
    /// </summary>
    public class FileEntry
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Constructs a FileEntry object from a Stream.
        ///     The finalizer for this class Disposes the <see cref="Content"/> unless <see cref="DisposeOnFinalize"/> is false.
        /// </summary>
        /// <param name="name">The logical path that identifies the contents in the FileEntry, used to construct <see cref="FullPath"/></param>
        /// <param name="inputStream">The stream to use for <see cref="Content"/></param>
        /// <param name="parent">The parent <see cref="FileEntry"/> this was extracted from</param>
        /// <param name="passthroughStream">If <see cref="passthroughStream"/> is true and <see cref="inputStream"/> is seekable will use the provided stream directly.
        ///     Otherwise, the full contents of <see cref="inputStream"/> is copied to a new internal <see cref="Stream"/></param>
        /// <param name="createTime">Specify the <see cref="CreateTime"/></param>
        /// <param name="modifyTime">Specify the <see cref="ModifyTime"/></param>
        /// <param name="accessTime">Specify the <see cref="AccessTime"/></param>
        /// <param name="memoryStreamCutoff">When specified, if <see cref="inputStream"/> is of length less than this value a <see cref="MemoryStream"/> will be used for backing.
        /// If it is greater than this value instead a <see cref="FileStream"/> with DeleteOnClose will be used instead. If unspecified, always use <see cref="MemoryStream"/></param>
        public FileEntry(string name, Stream inputStream, FileEntry? parent = null, bool passthroughStream = false, DateTime? createTime = null, DateTime? modifyTime = null, DateTime? accessTime = null, int? memoryStreamCutoff = null)
        {
            memoryStreamCutoff ??= defaultCutoff;

            Parent = parent;
            Passthrough = passthroughStream;

            CreateTime = createTime ?? DateTime.MinValue;
            ModifyTime = modifyTime ?? DateTime.MinValue;
            AccessTime = accessTime ?? DateTime.MinValue;

            Name = Path.GetFileName(name);

            if (parent == null)
            {
                FullPath = name;
            }
            else
            {
                FullPath = Path.Combine(parent.FullPath,name);
            }
            var printPath = FullPath;
            FullPath = ZipSlipSanitize(FullPath);

            if (inputStream == null)
            {
                throw new ArgumentNullException(nameof(inputStream));
            }

            if (!inputStream.CanRead)
            {
                Content = new MemoryStream();
            }

            // We want to be able to seek, so ensure any passthrough stream is Seekable
            if (passthroughStream && inputStream.CanSeek)
            {
                Content = inputStream;
                if (Content.Position != 0)
                {
                    Content.Position = 0;
                }
            }
            else
            {
                try
                {
                    if (inputStream.Length > memoryStreamCutoff)
                    {
                        Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                    }
                    else
                    {
                        Content = new MemoryStream();
                    }
                }
                catch (Exception)
                {
                    Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                }

                long? initialPosition = null;

                if (inputStream.CanSeek)
                {
                    initialPosition = inputStream.Position;
                    if (inputStream.Position != 0)
                    {
                        inputStream.Position = 0;
                    }
                }

                try
                {
                    inputStream.CopyTo(Content);
                }
                catch (NotSupportedException)
                {
                    try
                    {
                        Task.Run(() => inputStream.CopyToAsync(Content)).Wait();
                    }
                    catch (Exception f)
                    {
                        Logger.Debug("Failed to copy stream from {0} ({1}:{2})", printPath, f.GetType(), f.Message);
                    }
                }
                catch (Exception e)
                {
                    EntryStatus = FileEntryStatus.FailedFile;
                    Logger.Debug("Failed to copy stream from {0} ({1}:{2})", printPath, e.GetType(), e.Message);
                }

                if (inputStream.CanSeek && inputStream.Position != 0)
                {
                    inputStream.Position = initialPosition ?? 0;
                }

                Content.Position = 0;
            }
        }

        /// <summary>
        /// Uses MiniMagic to check the binary signature of the Content and return the detected Archive Type
        /// </summary>
        public ArchiveFileType ArchiveType => MiniMagic.DetectFileType(Content);

        /// <summary>
        /// The Contents of the File
        /// </summary>
        public Stream Content { get; }
        /// <summary>
        /// The Full Path to the File
        /// </summary>
        public string FullPath { get; }
        /// <summary>
        /// The relative path of the file in the Archive.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The Parent entry of this File.  For example, the Archive it came from.
        /// </summary>
        public FileEntry? Parent { get; }
        /// <summary>
        /// The Path to the parent.
        /// </summary>
        [Obsolete("If needed, access the Parent object's FullName directly. May be subject to removal in a future release. ")]
        public string? ParentPath => Parent?.FullPath;
        /// <summary>
        /// Should the <see cref="Content"/> Stream be disposed when this object is finalized.
        /// Default: true
        /// </summary>
        public bool DisposeOnFinalize { get; set; } = true;
        /// <summary>
        /// The Creation time of the file or DateTime.MinValue if unavailable
        /// </summary>
        public DateTime CreateTime { get; }
        /// <summary>
        /// The Modify time of the file or DateTime.MinValue if unavailable
        /// </summary>
        public DateTime ModifyTime { get; }
        /// <summary>
        /// The Access time of the file or DateTime.MinValue if unavailable
        /// </summary>
        public DateTime AccessTime { get; }
        /// <summary>
        /// ExtractionStatus metadata.
        /// </summary>
        public FileEntryStatus EntryStatus { get; set; }
        
        /// <summary>
        /// Regular expression to find characters that are not valid in filenames/paths on this system.
        /// Uses <see cref="Path.GetInvalidFileNameChars"/> excluding <see cref="Path.DirectorySeparatorChar"/>
        ///  so it can be run on full paths including existing separators we want to retain.
        /// </summary>
        private static readonly Regex InvalidFileChars = new Regex(
            $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars().Where(x => x != Path.DirectorySeparatorChar).ToArray()))}]");

        /// <summary>
        /// Sanitizes the <see cref="FullPath"/> from the values of <see cref="Path.GetInvalidFileNameChars"/>. Leveraged by the ExtractToDirectory methods of <see cref="Extractor"/>
        /// </summary>
        /// <param name="replacement">The string value to replace any invalid characters with</param>
        /// <returns>A sanitized path suitable to attempt to write to disk.</returns>
        public string GetSanitizedPath(string replacement = "_") => InvalidFileChars.Replace(FullPath, replacement);
        
        internal bool Passthrough { get; }

        private const int bufferSize = 4096;

        /// <summary>
        /// The deconstructor will dispose the <see cref="Content"/> stream if <see cref="DisposeOnFinalize"/> is set.
        /// </summary>
        ~FileEntry()
        {
            if (DisposeOnFinalize)
            {
                Content?.Dispose();
            }
        }

        /// <summary>
        /// Construct a FileEntry from a Stream Asynchronously
        /// </summary>
        /// <param name="name">The logical path that identifies the contents in the FileEntry, used to construct <see cref="FullPath"/></param>
        /// <param name="content">The stream to use for <see cref="Content"/></param>
        /// <param name="parent">The parent <see cref="FileEntry"/> this was extracted from</param>
        /// <param name="createTime">Specify the <see cref="CreateTime"/></param>
        /// <param name="modifyTime">Specify the <see cref="ModifyTime"/></param>
        /// <param name="accessTime">Specify the <see cref="AccessTime"/></param>
        /// <param name="memoryStreamCutoff">When specified, if <see cref="content"/> is of length less than this value a <see cref="MemoryStream"/> will be used for backing.
        /// If it is greater than this value instead a <see cref="FileStream"/> with DeleteOnClose will be used instead. If unspecified, always use <see cref="MemoryStream"/></param>
        /// <returns>A FileEntry object holding a Copy of the Stream as its <see cref="Content"/></returns>
        public static async Task<FileEntry> FromStreamAsync(string name, Stream content, FileEntry? parent = null, DateTime? createTime = null, DateTime? modifyTime = null, DateTime? accessTime = null, int? memoryStreamCutoff = null)
        {
            var status = FileEntryStatus.Default;
            memoryStreamCutoff ??= defaultCutoff;
            if (!content.CanRead || content == null)
            {
                content = new MemoryStream();
            }

            // Used for Debug statements
            string printPath;

            if (parent?.FullPath == null)
            {
                printPath = name;
            }
            else
            {
                printPath = Path.Combine(parent.FullPath, name);
            }
            Stream Content;
            try
            {
                if (content.Length > memoryStreamCutoff)
                {
                    Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                }
                else
                {
                    Content = new MemoryStream();
                }
            }
            catch (Exception)
            {
                Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            }

            long? initialPosition = null;

            if (content.CanSeek)
            {
                initialPosition = content.Position;
                if (content.Position != 0)
                {
                    content.Position = 0;
                }
            }

            try
            {
                await content.CopyToAsync(Content).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                try
                {
                    content.CopyTo(Content);
                }
                catch (Exception f)
                {
                    Logger.Debug("Failed to copy stream from {0} ({1}:{2})", printPath, f.GetType(), f.Message);
                }
            }
            catch (Exception e)
            {
                status = FileEntryStatus.FailedFile;
                Logger.Debug("Failed to copy stream from {0} ({1}:{2})", printPath, e.GetType(), e.Message);
            }

            if (content.CanSeek && content.Position != 0)
            {
                content.Position = initialPosition ?? 0;
            }

            Content.Position = 0;
            return new FileEntry(name, Content, parent, true, createTime, modifyTime, accessTime) { EntryStatus = status };
        }

        /// <summary>
        /// Replace .. for ZipSlip and remove any doubled up directory separators as a result - https://snyk.io/research/zip-slip-vulnerability
        /// </summary>
        /// <param name="fullPath">The path to sanitize</param>
        /// <param name="replacement">The string to replace .. with</param>
        /// <returns>A path without ZipSlip</returns>
        private static string ZipSlipSanitize(string fullPath, string replacement = "")
        {
            if (fullPath.Contains(".."))
            {
                Logger.Info("ZipSlip detected in {Path}. Removing unsafe path elements and extracting", fullPath);
                fullPath = fullPath.Replace("..", replacement);
                var doubleSeparator = $"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}";
                while (fullPath.Contains(doubleSeparator))
                {
                    fullPath = fullPath.Replace(doubleSeparator, $"{Path.DirectorySeparatorChar}");
                }
            }

            return fullPath;
        }

        private const int defaultCutoff = 1024 * 1024 * 100;

        /// <summary>
        /// Automatically open a Stream from a the <paramref name="filename"/> of a file on disc and construct a FileEntry suitable for Extracting from.
        /// </summary>
        /// <param name="filename">The path to the file on disc</param>
        /// <param name="memoryStreamCutoff">Passed through to the <see cref="FileEntry"/> constructor</param>
        /// <returns></returns>
        public static FileEntry FromFileName(string filename, int? memoryStreamCutoff = null)
        {
            return new FileEntry(Path.GetFileName(filename), File.OpenRead(filename), memoryStreamCutoff: memoryStreamCutoff, passthroughStream: true, createTime: File.GetCreationTime(filename), accessTime: File.GetLastAccessTime(filename), modifyTime: File.GetLastWriteTime(filename));
        }
    }

    /// <summary>
    /// Status information about the provenance of this <see cref="FileEntry"/>
    /// </summary>
    public enum FileEntryStatus
    {
        /// <summary>
        /// Status has not been set. Implies no issues.
        /// </summary>
        Default,
        /// <summary>
        /// Indicates that creation of this FileEntry was unsuccessful and <see cref="FileEntry.Content"/> for this FileEntry will be empty.
        /// </summary>
        FailedFile,
        /// <summary>
        /// Indicates that <see cref="FileEntry.Content"/> stream contains an archive which failed to extract. To have failed archives returned as FileEntries from extractors use <see cref="ExtractorOptions.ExtractSelfOnFail"/>.
        /// </summary>
        FailedArchive,
        /// <summary>
        /// Indicates that <see cref="FileEntry.Content"/> contains an archive which failed to decrypt. To have encrypted archives returned as FileEntries from extractors use <see cref="ExtractorOptions.ExtractSelfOnFail"/>.
        /// </summary>
        EncryptedArchive
    }
}