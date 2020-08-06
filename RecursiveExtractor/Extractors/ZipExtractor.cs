using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class ZipExtractor : ExtractorImplementation
    {
        public ZipExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.ZIP;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }


        /// <summary>
        ///     Extracts an zip file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            ZipFile? zipFile = null;
            try
            {
                zipFile = new ZipFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (zipFile != null)
            {
                if (options.Parallel)
                {
                    var files = new ConcurrentStack<FileEntry>();

                    var zipEntries = new List<ZipEntry>();
                    foreach (ZipEntry? zipEntry in zipFile)
                    {
                        if (zipEntry is null ||
                            zipEntry.IsDirectory ||
                            zipEntry.IsCrypted ||
                            !zipEntry.CanDecompress)
                        {
                            continue;
                        }
                        var fei = new FileEntryInfo(zipEntry.Name, fileEntry.FullPath, zipEntry.Size);
                        zipEntries.Add(zipEntry);
                    }

                    while (zipEntries.Count > 0)
                    {
                        var batchSize = Math.Min(options.BatchSize, zipEntries.Count);
                        var selectedEntries = zipEntries.GetRange(0, batchSize);
                        governor.CheckResourceGovernor(selectedEntries.Sum(x => x.Size));
                        try
                        {
                            selectedEntries.AsParallel().ForAll(zipEntry =>
                            {
                                try
                                {
                                    var zipStream = zipFile.GetInputStream(zipEntry);
                                    var newFileEntry = new FileEntry(zipEntry.Name, zipStream, fileEntry);
                                    if (Extractor.IsQuine(newFileEntry))
                                    {
                                        Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                                        governor.CurrentOperationProcessedBytesLeft = -1;
                                    }
                                    else
                                    {
                                        files.PushRange(Context.ExtractFile(newFileEntry, options, governor).ToArray());
                                    }
                                }
                                catch (Exception e) when (e is OverflowException)
                                {
                                    Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                                    throw;
                                }
                                catch (Exception e)
                                {
                                    Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                                }
                            });
                        }
                        catch (Exception e) when (e is AggregateException)
                        {
                            if (e.InnerException?.GetType() == typeof(OverflowException))
                            {
                                throw e.InnerException;
                            }
                            throw;
                        }

                        governor.CheckResourceGovernor(0);
                        zipEntries.RemoveRange(0, batchSize);

                        while (files.TryPop(out var result))
                        {
                            if (result != null)
                                yield return result;
                        }
                    }
                }
                else
                {
                    foreach (ZipEntry? zipEntry in zipFile)
                    {
                        if (zipEntry is null ||
                            zipEntry.IsDirectory ||
                            zipEntry.IsCrypted ||
                            !zipEntry.CanDecompress)
                        {
                            continue;
                        }

                        governor.CheckResourceGovernor(zipEntry.Size);

                        using var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
                        try
                        {
                            var buffer = new byte[BUFFER_SIZE];
                            var zipStream = zipFile.GetInputStream(zipEntry);
                            StreamUtils.Copy(zipStream, fs, buffer);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.ZIP, fileEntry.FullPath, zipEntry.Name, e.GetType());
                        }

                        var newFileEntry = new FileEntry(zipEntry.Name, fs, fileEntry);

                        if (Extractor.IsQuine(newFileEntry))
                        {
                            Logger.Info(Extractor.IS_QUINE_STRING, fileEntry.Name, fileEntry.FullPath);
                            throw new OverflowException();
                        }

                        foreach (var extractedFile in Context.ExtractFile(newFileEntry, options, governor))
                        {
                            yield return extractedFile;
                        }
                    }
                }
            }
        }

    }
}
