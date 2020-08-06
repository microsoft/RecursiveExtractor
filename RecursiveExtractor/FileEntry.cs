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
            Name = name;
            Passthrough = passthroughStream;

            if (parent == null)
            {
                ParentPath = null;
                FullPath = Name;
            }
            else
            {
                ParentPath = parent.FullPath;
                FullPath = $"{ParentPath}/{Name}";
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

        public Stream Content { get; }
        public string FullPath { get; }
        public string Name { get; }
        public FileEntry? Parent { get; }
        public string? ParentPath { get; }

        ~FileEntry()
        {
            if (!Passthrough)
            {
                Content?.Dispose();
            }
        }

        public bool Passthrough { get; }

        internal static async Task<FileEntry> FromStreamAsync(string name, Stream content, FileEntry? parent)
        {
            if (!content.CanRead || content == null)
            {
                content = new MemoryStream();
            }
            string? ParentPath = null;
            string FullPath = string.Empty;
            if (parent == null)
            {
                ParentPath = null;
                FullPath = name;
            }
            else
            {
                ParentPath = parent.FullPath;
                FullPath = $"{ParentPath}/{name}";
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