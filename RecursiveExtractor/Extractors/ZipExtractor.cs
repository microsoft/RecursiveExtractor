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

                // Re-iterate through entries with the correct archive instance
                foreach (var zipEntry in zipArchive.Entries.Where(e => !e.IsDirectory))
                {
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

                // Re-iterate through entries with the correct archive instance
                foreach (var zipEntry in zipArchive.Entries.Where(e => !e.IsDirectory))
                {
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
            }
        }

        private const int bufferSize = 4096;
    }
}
