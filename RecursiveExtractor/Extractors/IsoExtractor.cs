using DiscUtils;
using DiscUtils.Iso9660;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class IsoExtractor : ExtractorImplementation
    {
        public IsoExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.ISO_9660;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts an an ISO file
        /// </summary>
        /// <param name="fileEntry"> </param>
        /// <returns> </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            using var cd = new CDReader(fileEntry.Content, true);
            var entries = cd.GetFiles(cd.Root.FullName, "*.*", SearchOption.AllDirectories);
            if (entries != null)
            {
                    foreach (var file in entries)
                    {
                        var fileInfo = cd.GetFileInfo(file);
                        governor.CheckResourceGovernor(fileInfo.Length);
                        Stream? stream = null;
                        try
                        {
                            var fei = new FileEntryInfo(fileInfo.Name, Path.Combine(fileEntry.FullPath, fileInfo.FullName), fileInfo.Length);
                            stream = fileInfo.OpenRead();
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Failed to extract {0} from ISO {1}. ({2}:{3})", fileInfo.Name, fileEntry.FullPath, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var newFileEntry = new FileEntry(fileInfo.Name, stream, fileEntry);
                            var innerEntries = Context.ExtractFile(newFileEntry, options, governor);
                            foreach (var entry in innerEntries)
                            {
                                yield return entry;
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
