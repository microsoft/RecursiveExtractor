using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Zip Archive Extraction Implementation
    /// </summary>
    public class ZipExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public ZipExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }
        private const int BUFFER_SIZE = 32768;

        private string? GetZipPassword(FileEntry fileEntry, IArchiveEntry zipEntry, ExtractorOptions options)
        {
            foreach (var passwords in options.Passwords.Where(x => x.Key.IsMatch(fileEntry.Name)))
            {
                foreach (var password in passwords.Value)
                {
                    try
                    {
                        // Create a new archive instance with the password to test it
                        fileEntry.Content.Position = 0;
                        using var testArchive = ZipArchive.Open(fileEntry.Content, new ReaderOptions() 
                        { 
                            Password = password,
                            LeaveStreamOpen = true
                        });
                        
                        // Try to get the input stream for the entry to verify password
                        var testEntry = testArchive.Entries.FirstOrDefault(e => e.Key == zipEntry.Key);
                        if (testEntry != null)
                        {
                            using var testStream = testEntry.OpenEntryStream();
                            // If we can read without exception, password is correct
                            var buffer = new byte[1];
                            testStream.ReadExactly(buffer, 0, 1);
                            return password;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(Extractor.FAILED_PASSWORD_ERROR_MESSAGE_STRING, fileEntry.FullPath, ArchiveFileType.ZIP, e.GetType(), e.Message);
                    }
                }
            }
            return null;
        }

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
                ///<inheritdoc />
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            ZipArchive? zipArchive = null;
            try
            {
                fileEntry.Content.Position = 0;
                zipArchive = ZipArchive.Open(fileEntry.Content, new ReaderOptions() 
                { 
                    LeaveStreamOpen = true 
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipArchive is null)
            {
                fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                if (options.ExtractSelfOnFail)
                {
                    yield return fileEntry;
                }
            }
            else
            {
                var buffer = new byte[BUFFER_SIZE];
                
                // Check if we have any encrypted entries and get password if needed
                var firstEncryptedEntry = zipArchive.Entries.FirstOrDefault(e => !e.IsDirectory && e.IsEncrypted);
                if (firstEncryptedEntry != null)
                {
                    var foundPassword = GetZipPassword(fileEntry, firstEncryptedEntry, options);
                    if (foundPassword != null)
                    {
                        // Recreate archive with password
                        zipArchive.Dispose();
                        fileEntry.Content.Position = 0;
                        zipArchive = ZipArchive.Open(fileEntry.Content, new ReaderOptions() 
                        { 
                            Password = foundPassword,
                            LeaveStreamOpen = true 
                        });
                    }
                    else
                    {
                        fileEntry.EntryStatus = FileEntryStatus.EncryptedArchive;
                        if (options.ExtractSelfOnFail)
                        {
                            yield return fileEntry;
                        }
                        yield break;
                    }
                }

                // Gather the set of entry keys known to the central directory so we can
                // later compare against local headers to find non-indexed (hidden) content.
                var catalogedKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var zipEntry in zipArchive.Entries.Where(e => !e.IsDirectory))
                {
                    if (zipEntry.Key != null)
                        catalogedKeys.Add(zipEntry.Key);

                    governor.CheckResourceGovernor(zipEntry.Size);

                    Stream? target = null;
                    try
                    {
                        using var zipStream = zipEntry.OpenEntryStream();
                        target = StreamFactory.GenerateAppropriateBackingStream(options, zipStream);
                        await zipStream.CopyToAsync(target);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Key, e.GetType());
                    }

                    target ??= new MemoryStream();
                    var name = zipEntry.Key?.Replace('/', Path.DirectorySeparatorChar) ?? "";
                    var newFileEntry = new FileEntry(name, target, fileEntry, modifyTime: zipEntry.LastModifiedTime, memoryStreamCutoff: options.MemoryStreamCutoff);

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
                
                zipArchive?.Dispose();

                // When opted in, walk local file headers via a forward-only reader to discover
                // entries that are absent from the central directory (steganographic / tampered content).
                if (options.ExtractNonIndexedEntries)
                {
                    await foreach (var hiddenEntry in YieldNonIndexedEntriesAsync(fileEntry, catalogedKeys, options, governor, topLevel))
                    {
                        yield return hiddenEntry;
                    }
                }
            }
        }

        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
                ///<inheritdoc />
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            ZipArchive? zipArchive = null;
            try
            {
                fileEntry.Content.Position = 0;
                zipArchive = ZipArchive.Open(fileEntry.Content, new ReaderOptions() 
                { 
                    LeaveStreamOpen = true 
                });
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipArchive is null)
            {
                fileEntry.EntryStatus = FileEntryStatus.FailedArchive;
                if (options.ExtractSelfOnFail)
                {
                    yield return fileEntry;
                }
            }
            else
            {
                var buffer = new byte[BUFFER_SIZE];
                
                // Check if we have any encrypted entries and get password if needed
                var firstEncryptedEntry = zipArchive.Entries.FirstOrDefault(e => !e.IsDirectory && e.IsEncrypted);
                if (firstEncryptedEntry != null)
                {
                    var foundPassword = GetZipPassword(fileEntry, firstEncryptedEntry, options);
                    if (foundPassword != null)
                    {
                        // Recreate archive with password
                        zipArchive.Dispose();
                        fileEntry.Content.Position = 0;
                        zipArchive = ZipArchive.Open(fileEntry.Content, new ReaderOptions() 
                        { 
                            Password = foundPassword,
                            LeaveStreamOpen = true 
                        });
                    }
                    else
                    {
                        fileEntry.EntryStatus = FileEntryStatus.EncryptedArchive;
                        if (options.ExtractSelfOnFail)
                        {
                            yield return fileEntry;
                        }
                        yield break;
                    }
                }

                var catalogedKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var zipEntry in zipArchive.Entries.Where(e => !e.IsDirectory))
                {
                    if (zipEntry.Key != null)
                        catalogedKeys.Add(zipEntry.Key);

                    governor.CheckResourceGovernor(zipEntry.Size);

                    using var fs = StreamFactory.GenerateAppropriateBackingStream(options, zipEntry.Size);

                    try
                    {
                        using var zipStream = zipEntry.OpenEntryStream();
                        zipStream.CopyTo(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Key, e.GetType());
                    }

                    var name = zipEntry.Key?.Replace('/', Path.DirectorySeparatorChar) ?? "";
                    var newFileEntry = new FileEntry(name, fs, fileEntry, modifyTime: zipEntry.LastModifiedTime, memoryStreamCutoff: options.MemoryStreamCutoff);

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
                
                zipArchive?.Dispose();

                if (options.ExtractNonIndexedEntries)
                {
                    foreach (var hiddenEntry in YieldNonIndexedEntries(fileEntry, catalogedKeys, options, governor, topLevel))
                    {
                        yield return hiddenEntry;
                    }
                }
            }
        }

        private const int bufferSize = 4096;

        /// <summary>
        /// Uses the SharpCompress forward-only reader (which walks local file headers sequentially)
        /// to discover entries absent from the central directory. Yields them as FileEntries with
        /// <see cref="FileEntryStatus.NonIndexedEntry"/> status.
        /// </summary>
        private async IAsyncEnumerable<FileEntry> YieldNonIndexedEntriesAsync(
            FileEntry parentEntry,
            HashSet<string> catalogedKeys,
            ExtractorOptions options,
            ResourceGovernor governor,
            bool topLevel)
        {
            parentEntry.Content.Position = 0;
            IReader? forwardReader = null;
            try
            {
                forwardReader = ReaderFactory.Open(parentEntry.Content, new ReaderOptions { LeaveStreamOpen = true });
            }
            catch (Exception ex)
            {
                Logger.Debug("Non-indexed entry scan failed to open reader for {0}: {1}", parentEntry.FullPath, ex.GetType());
            }

            if (forwardReader == null)
                yield break;

            using (forwardReader)
            {
                while (true)
                {
                    bool advanced;
                    try { advanced = forwardReader.MoveToNextEntry(); }
                    catch (Exception ex)
                    {
                        Logger.Debug("Non-indexed entry scan encountered an error advancing reader for {0}: {1}", parentEntry.FullPath, ex.GetType());
                        break;
                    }

                    if (!advanced)
                        break;

                    var readerKey = forwardReader.Entry.Key;
                    if (forwardReader.Entry.IsDirectory || readerKey == null)
                        continue;

                    // Skip entries that the central directory already reported
                    if (catalogedKeys.Contains(readerKey))
                        continue;

                    Logger.Info("Discovered non-indexed ZIP entry '{0}' in {1}", readerKey, parentEntry.FullPath);

                    Stream? payload = null;
                    try
                    {
                        using var readerStream = forwardReader.OpenEntryStream();
                        governor.CheckResourceGovernor(forwardReader.Entry.Size);
                        payload = StreamFactory.GenerateAppropriateBackingStream(options, readerStream);
                        await readerStream.CopyToAsync(payload);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ZIP, parentEntry.FullPath, readerKey, ex.GetType());
                    }

                    payload ??= new MemoryStream();
                    var entryName = readerKey.Replace('/', Path.DirectorySeparatorChar);
                    var discovered = new FileEntry(entryName, payload, parentEntry, passthroughStream: true, memoryStreamCutoff: options.MemoryStreamCutoff)
                    {
                        EntryStatus = FileEntryStatus.NonIndexedEntry
                    };

                    if (options.Recurse || topLevel)
                    {
                        await foreach (var nested in Context.ExtractAsync(discovered, options, governor, false))
                        {
                            yield return nested;
                        }
                    }
                    else
                    {
                        yield return discovered;
                    }
                }
            }
        }

        /// <summary>
        /// Synchronous counterpart of <see cref="YieldNonIndexedEntriesAsync"/>.
        /// </summary>
        private IEnumerable<FileEntry> YieldNonIndexedEntries(
            FileEntry parentEntry,
            HashSet<string> catalogedKeys,
            ExtractorOptions options,
            ResourceGovernor governor,
            bool topLevel)
        {
            parentEntry.Content.Position = 0;
            IReader? forwardReader = null;
            try
            {
                forwardReader = ReaderFactory.Open(parentEntry.Content, new ReaderOptions { LeaveStreamOpen = true });
            }
            catch (Exception ex)
            {
                Logger.Debug("Non-indexed entry scan failed to open reader for {0}: {1}", parentEntry.FullPath, ex.GetType());
            }

            if (forwardReader == null)
                yield break;

            using (forwardReader)
            {
                while (true)
                {
                    bool advanced;
                    try { advanced = forwardReader.MoveToNextEntry(); }
                    catch (Exception ex)
                    {
                        Logger.Debug("Non-indexed entry scan encountered an error advancing reader for {0}: {1}", parentEntry.FullPath, ex.GetType());
                        break;
                    }

                    if (!advanced)
                        break;

                    var readerKey = forwardReader.Entry.Key;
                    if (forwardReader.Entry.IsDirectory || readerKey == null)
                        continue;

                    if (catalogedKeys.Contains(readerKey))
                        continue;

                    Logger.Info("Discovered non-indexed ZIP entry '{0}' in {1}", readerKey, parentEntry.FullPath);

                    Stream? payload = null;
                    try
                    {
                        using var readerStream = forwardReader.OpenEntryStream();
                        governor.CheckResourceGovernor(forwardReader.Entry.Size);
                        payload = StreamFactory.GenerateAppropriateBackingStream(options, readerStream);
                        readerStream.CopyTo(payload);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(Extractor.FAILED_PARSING_ERROR_MESSAGE_STRING, ArchiveFileType.ZIP, parentEntry.FullPath, readerKey, ex.GetType());
                    }

                    payload ??= new MemoryStream();

                    var entryName = readerKey.Replace('/', Path.DirectorySeparatorChar);
                    var discovered = new FileEntry(entryName, payload, parentEntry, passthroughStream: true, memoryStreamCutoff: options.MemoryStreamCutoff)
                    {
                        EntryStatus = FileEntryStatus.NonIndexedEntry
                    };

                    if (options.Recurse || topLevel)
                    {
                        foreach (var nested in Context.Extract(discovered, options, governor, false))
                        {
                            yield return nested;
                        }
                    }
                    else
                    {
                        yield return discovered;
                    }
                }
            }
        }
    }
}
