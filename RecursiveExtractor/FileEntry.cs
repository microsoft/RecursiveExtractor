// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor
{
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
        /// <param name="parentPath"> </param>
        /// <param name="inputStream"> </param>
        /// <param name="parent"> </param>
        /// <param name="passthroughStream"> </param>
        public FileEntry(string name, Stream inputStream, FileEntry? parent = null, bool passthroughStream = false)
        {
            Parent = parent;
            Passthrough = passthroughStream;

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
                // Back with a temporary filestream, this is optimized to be cached in memory when possible
                // automatically by .NET
                Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
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
                        System.Threading.Tasks.Task.Run(() => inputStream.CopyToAsync(Content)).Wait();
                    }
                    catch (Exception f)
                    {
                        var message = f.Message;
                        var type = f.GetType();
                        Logger.Debug("Failed to copy stream from {0} ({1}:{2})", FullPath, f.GetType(), f.Message);
                    }
                }
                catch (Exception e)
                {
                    Logger.Debug("Failed to copy stream from {0} ({1}:{2})", FullPath, e.GetType(), e.Message);
                }

                if (inputStream.CanSeek && inputStream.Position != 0)
                {
                    inputStream.Position = initialPosition ?? 0;
                }

                Content.Position = 0;
            }
        }

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
        /// Should the Content Stream be disposed when this object is finalized.
        /// Default: true
        /// </summary>
        public bool DisposeOnFinalize { get; set; } = true;

        internal bool Passthrough { get; }

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
        /// <returns>A FileEntry object holding a Copy of the Stream</returns>
        public static async Task<FileEntry> FromStreamAsync(string name, Stream content, FileEntry? parent)
        {
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

            // Back with a temporary filestream, this is optimized to be cached in memory when possible
            // automatically by .NET
            var Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
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
                await content.CopyToAsync(Content);
            }
            catch (NotSupportedException)
            {
                try
                {
                    content.CopyTo(Content);
                }
                catch (Exception f)
                {
                    var message = f.Message;
                    var type = f.GetType();
                    Logger.Debug("Failed to copy stream from {0} ({1}:{2})", FullPath, f.GetType(), f.Message);
                }
            }
            catch (Exception e)
            {
                Logger.Debug("Failed to copy stream from {0} ({1}:{2})", FullPath, e.GetType(), e.Message);
            }

            if (content.CanSeek && content.Position != 0)
            {
                content.Position = initialPosition ?? 0;
            }

            Content.Position = 0;

            return new FileEntry(name, Content, parent, true);
        }
    }
}