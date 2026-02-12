using SharpCompress.Archives.Rar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The RAR Archive extractor implementation
    /// </summary>
    public class RarExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public RarExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        private (RarArchive? archive, FileEntryStatus archiveStatus) GetRarArchive(FileEntry fileEntry, ExtractorOptions options)
        {
            RarArchive? rarArchive = null;
            var needsPassword = false;

            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
                // Test for invalid archives. This will throw invalidformatexception
                var t = rarArchive.IsSolid;
                if (rarArchive.Entries.Any(x => x.IsEncrypted))
                {
                    needsPassword = true;
                }
            }
            catch (SharpCompress.Common.CryptographicException)
            {
                needsPassword = true;
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, fileEntry.ArchiveType, fileEntry.FullPath, string.Empty, e.GetType());
                return (null, FileEntryStatus.FailedArchive);
            }

            // RAR5 encryption is not supported by SharpCompress
            if (needsPassword && fileEntry.ArchiveType == ArchiveFileType.RAR5)
            {
                return (null, FileEntryStatus.EncryptedArchive);
            }
            
            if (needsPassword)
            {
                var passwordFound = false;
                foreach (var passwords in options.Passwords.Where(x => x.Key.IsMatch(fileEntry.Name)))
                {
                    if (passwordFound) { break; }
                    foreach (var password in passwords.Value)
                    {
                        try
                        {
                            fileEntry.Content.Position = 0;
                            rarArchive = RarArchive.Open(fileEntry.Content, new SharpCompress.Readers.ReaderOptions() { Password = password, LookForHeader = true });
                            var byt = new byte[1];
                            var encryptedEntry = rarArchive.Entries.FirstOrDefault(x => x is { IsEncrypted: true, Size: > 0 });
                            // Justification for !: Because we use FirstOrDefault encryptedEntry may be null, but we have a catch below for it
                            using var entryStream = encryptedEntry!.OpenEntryStream();
                            if (entryStream.Read(byt, 0, 1) > 0)
                            {
                                passwordFound = true;
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Trace(Extractor.FAILED_PASSWORD_ERROR_MESSAGE_STRING, fileEntry.FullPath, ArchiveFileType.RAR, e.GetType(), e.Message);
                        }
                    }
                }
                if (!passwordFound)
                {
                    return (null, FileEntryStatus.EncryptedArchive);
                }
            }
            return (rarArchive, FileEntryStatus.Default);
        }

        /// <summary>
        ///     Extracts a RAR archive
        /// </summary>
        ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var rarArchive, var archiveType) = GetRarArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveType;
            if (rarArchive != null && fileEntry.EntryStatus == FileEntryStatus.Default)
            {
                foreach (var entry in rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory))
                {
                    governor.CheckResourceGovernor(entry.Size);
                    var name = (entry.Key ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                    var newFileEntry = await FileEntry.FromStreamAsync(name, entry.OpenEntryStream(), fileEntry, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff).ConfigureAwait(false);
                    if (newFileEntry != null)
                    {
                        try
                        {
                            if (entry.Attrib.HasValue)
                            {
                                newFileEntry.Metadata = new FileEntryMetadata { Mode = entry.Attrib.Value };
                            }
                        }
                        catch (Exception e) { Logger.Trace("Failed to read file attributes: {0}", e.Message); }
                        if (options.Recurse || topLevel)
                        {
                            await foreach (var innerEntry in Context.ExtractAsync(newFileEntry, options, governor, false))
                            {
                                yield return innerEntry;
                            }
                        }
                        else
                        {
                            yield return newFileEntry;
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
        ///     Extracts a RAR archive
        /// </summary>
        ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            (var rarArchive, var archiveType) = GetRarArchive(fileEntry, options);
            fileEntry.EntryStatus = archiveType;
            if (rarArchive != null && fileEntry.EntryStatus == FileEntryStatus.Default)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory);
                foreach (var entry in entries)
                {
                    governor.CheckResourceGovernor(entry.Size);
                    FileEntry? newFileEntry = null;
                    try
                    {
                        var stream = entry.OpenEntryStream();
                        var name = (entry.Key ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                        newFileEntry = new FileEntry(name, stream, fileEntry, false, entry.CreatedTime, entry.LastModifiedTime, entry.LastAccessedTime, memoryStreamCutoff: options.MemoryStreamCutoff);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                    }
                    if (newFileEntry != null)
                    {
                        try
                        {
                            if (entry.Attrib.HasValue)
                            {
                                newFileEntry.Metadata = new FileEntryMetadata { Mode = entry.Attrib.Value };
                            }
                        }
                        catch (Exception e) { Logger.Trace("Failed to read file attributes: {0}", e.Message); }
                        if (options.Recurse || topLevel)
                        {
                            foreach (var innerEntry in Context.Extract(newFileEntry, options, governor, false))
                            {
                                yield return innerEntry;
                            }
                        }
                        else
                        {
                            yield return newFileEntry;
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
    }
}