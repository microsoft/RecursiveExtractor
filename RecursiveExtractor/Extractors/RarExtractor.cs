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

        private RarArchive? GetRarArchive(FileEntry fileEntry, ExtractorOptions options)
        {
            RarArchive? rarArchive = null;
            try
            {
                rarArchive = RarArchive.Open(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (rarArchive is null)
            {
                return null;
            }
            var needsPassword = false;
            try
            {
                using var testStream = rarArchive.Entries.First().OpenEntryStream();
            }
            catch (Exception)
            {
                needsPassword = true;
            }
            if (needsPassword is true)
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
                            var count = 0; //To do something in the loop
                            foreach(var entry in rarArchive.Entries)
                            {
                                //Just do anything in the loop, but you need to loop over entries to check if the password is correct
                                count++; 
                            }
                            break;
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, string.Empty, e.GetType());
                        }
                    }
                }
            }
            return rarArchive;
        }

        /// <summary>
        ///     Extracts an a RAR archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            var rarArchive = GetRarArchive(fileEntry, options);
            if (rarArchive != null)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory);

                foreach (var entry in entries)
                {
                    governor.CheckResourceGovernor(entry.Size);
                    var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                    FileEntry newFileEntry = await FileEntry.FromStreamAsync(name, entry.OpenEntryStream(), fileEntry);
                    if (newFileEntry != null)
                    {
                        if (Extractor.IsQuine(newFileEntry))
                        {
                            Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }
                        await foreach (var extractedFile in Context.ExtractAsync(newFileEntry, options, governor))
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
        ///     Extracts an a RAR archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            var rarArchive = GetRarArchive(fileEntry, options);
            if (rarArchive != null)
            {
                var entries = rarArchive.Entries.Where(x => x.IsComplete && !x.IsDirectory);
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
                                if (Extractor.IsQuine(newFileEntry))
                                {
                                    Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                    governor.CurrentOperationProcessedBytesLeft = -1;
                                }
                                else
                                {
                                    files.PushRange(Context.Extract(newFileEntry, options, governor).ToArray());
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, streampair.entry.Key, e.GetType());
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
                            var name = entry.Key.Replace('/', Path.DirectorySeparatorChar);

                            newFileEntry = new FileEntry(name, entry.OpenEntryStream(), fileEntry);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.RAR, fileEntry.FullPath, entry.Key, e.GetType());
                        }
                        if (newFileEntry != null)
                        {
                            if (Extractor.IsQuine(newFileEntry))
                            {
                                Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                throw new OverflowException();
                            }
                            foreach (var extractedFile in Context.Extract(newFileEntry, options, governor))
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
    }
}
