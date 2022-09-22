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
        ///     Constructs a FileEntry object from a Stream. If passthroughStream is set to true, and the
        ///     stream is seekable, it will directly use inputStream. If passthroughStream is false or it is
        ///     not seekable, it will copy the full contents of inputStream to a new internal FileStream and
        ///     attempt to reset the position of inputstream. The finalizer for this class Disposes the
        ///     contained Stream.
        /// </summary>
        /// <param name="name"> </param>
        /// <param name="inputStream"> </param>
        /// <param name="parent"> </param>
        /// <param name="passthroughStream"> </param>
        /// <param name="createTime"></param>
        /// <param name="modifyTime"></param>
        /// <param name="accessTime"></param>
        /// <param name="memoryStreamCutoff">Size in bytes for maximum size to back with MemoryStream instead of ephemeral FileStream</param>
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
                ParentPath = null;
                FullPath = name;
            }
            else
            {
                ParentPath = parent.FullPath;
                FullPath = $"{ParentPath}{Path.DirectorySeparatorChar}{name}";
            }
            var printPath = FullPath;
            if (FullPath.Contains(".."))
            {
                Logger.Info("ZipSlip detected in {0}. Removing unsafe path elements and extracting.", FullPath);
                // Replace .. for ZipSlip - https://snyk.io/research/zip-slip-vulnerability
                FullPath = FullPath.Replace("..", "");
                var doubleSeparator = $"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}";
                while (FullPath.Contains(doubleSeparator))
                {
                    FullPath = FullPath.Replace(doubleSeparator, $"{Path.DirectorySeparatorChar}");
                }
            }

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
        public string? ParentPath { get; }
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
        
        private static Regex invalidPathChars = new Regex(string.Format("[{0}]", Regex.Escape(new string(System.IO.Path.GetInvalidPathChars()))));
        private static Regex invalidFileChars = new Regex(string.Format("[{0}]", Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()))));
        
        /// <summary>
        /// Returns the full sanitized path of the FileEntry suitable to write to disk
        /// </summary>
        /// <returns></returns>
        public string GetSanitizedPath()
        {
            var bits = FullPath.Split(new[]{Path.DirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < bits.Length - 1; i++)
            {
                bits[i] = invalidFileChars.Replace(bits[i], "_");
            }
            bits[^1] = invalidFileChars.Replace(bits[^1], "_");
            return Path.Combine(bits);
        }

        /// <summary>
        /// Returns the sanitized filename the FileEntry suitable to write to disk
        /// </summary>
        /// <returns></returns>
        public string GetSanitizedName()
        {
            return invalidFileChars.Replace(Name, "_");
        }
        
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
        /// <param name="name">Name of the FileEntry</param>
        /// <param name="content">The Stream to parse</param>
        /// <param name="parent">The Parent FileEntry</param>
        /// <param name="createTime"></param>
        /// <param name="modifyTime"></param>
        /// <param name="accessTime"></param>
        /// <param name="memoryStreamCutoff">Size in bytes for maximum size to back with MemoryStream instead of ephemeral FileStream</param>
        /// <returns>A FileEntry object holding a Copy of the Stream</returns>
        public static async Task<FileEntry> FromStreamAsync(string name, Stream content, FileEntry? parent = null, DateTime? createTime = null, DateTime? modifyTime = null, DateTime? accessTime = null, int? memoryStreamCutoff = null)
        {
            var status = FileEntryStatus.Default;
            memoryStreamCutoff ??= defaultCutoff;
            if (!content.CanRead || content == null)
            {
                content = new MemoryStream();
            }

            // Used for Debug statements
            string FullPath;

            if (parent == null)
            {
                FullPath = name;
            }
            else
            {
                FullPath = $"{parent?.FullPath}{Path.DirectorySeparatorChar}{name}";
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
                    Logger.Debug("Failed to copy stream from {0} ({1}:{2})", FullPath, f.GetType(), f.Message);
                }
            }
            catch (Exception e)
            {
                status = FileEntryStatus.FailedFile;
                Logger.Debug("Failed to copy stream from {0} ({1}:{2})", FullPath, e.GetType(), e.Message);
            }

            if (content.CanSeek && content.Position != 0)
            {
                content.Position = initialPosition ?? 0;
            }

            Content.Position = 0;
            return new FileEntry(name, Content, parent, true, createTime, modifyTime, accessTime) { EntryStatus = status };
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