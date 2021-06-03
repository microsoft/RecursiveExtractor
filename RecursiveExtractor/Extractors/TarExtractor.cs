using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The Tar archive extractor implementation
    /// </summary>
    public class TarExtractor : AsyncExtractorInterface
    {
        /// <summary>
        /// The constructor takes the Extractor context for recursion.
        /// </summary>
        /// <param name="context">The Extractor context.</param>
        public TarExtractor(Extractor context)
        {
            Context = context;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an a Tar archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            TarEntry tarEntry;
            TarInputStream? tarStream = null;
            try
            {
                tarStream = new TarInputStream(fileEntry.Content, System.Text.Encoding.UTF8);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (tarStream != null)
            {
                while ((tarEntry = tarStream.GetNextEntry()) != null)
                {
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }

                    var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                    governor.CheckResourceGovernor(tarStream.Length);
                    try
                    {
                        tarStream.CopyEntryContents(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Name, e.GetType());
                    }

                    var name = tarEntry.Name.Replace('/', Path.DirectorySeparatorChar);
                    if (options.FileNamePasses($"{fileEntry.FullPath}{Path.DirectorySeparatorChar}{name}"))
                    {
                        var newFileEntry = new FileEntry(name, fs, fileEntry, true, memoryStreamCutoff: options.MemoryStreamCutoff);

                        if (newFileEntry != null)
                        {
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
                tarStream.Dispose();
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
        ///     Extracts an a Tar archive
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            TarEntry tarEntry;
            TarInputStream? tarStream = null;
            try
            {
                tarStream = new TarInputStream(fileEntry.Content, System.Text.Encoding.UTF8);
            }
            catch (Exception e)
            {
                Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, string.Empty, e.GetType());
            }
            if (tarStream != null)
            {
                while ((tarEntry = tarStream.GetNextEntry()) != null)
                {
                    if (tarEntry.IsDirectory)
                    {
                        continue;
                    }

                    var fs = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, FileOptions.DeleteOnClose);
                    governor.CheckResourceGovernor(tarStream.Length);
                    try
                    {
                        tarStream.CopyEntryContents(fs);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(Extractor.DEBUG_STRING, ArchiveFileType.TAR, fileEntry.FullPath, tarEntry.Name, e.GetType());
                    }
                    var name = tarEntry.Name.Replace('/', Path.DirectorySeparatorChar);
                    if (options.FileNamePasses($"{fileEntry.FullPath}{Path.DirectorySeparatorChar}{name}"))
                    {
                        var newFileEntry = new FileEntry(name, fs, fileEntry, true, memoryStreamCutoff: options.MemoryStreamCutoff);

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
                tarStream.Dispose();
            }
            else
            {
                if (options.ExtractSelfOnFail)
                {
                    yield return fileEntry;
                }
            }
        }

        private const int bufferSize = 4096;
    }
}
