using DiscUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public class WimExtractor : ExtractorImplementation
    {
        public WimExtractor(Extractor context)
        {
            Context = context;
            TargetType = ArchiveFileType.WIM;
        }
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal Extractor Context { get; }

        /// <summary>
        ///     Extracts a WIM file contained in fileEntry.
        /// </summary>
        /// <param name="fileEntry"> FileEntry to extract </param>
        /// <returns> Extracted files </returns>
        public override IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor)
        {
            DiscUtils.Wim.WimFile? baseFile = null;
            try
            {
                baseFile = new DiscUtils.Wim.WimFile(fileEntry.Content);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to init WIM image.");
            }
            if (baseFile != null)
            {
                for (var i = 0; i < baseFile.ImageCount; i++)
                {
                    var image = baseFile.GetImage(i);
                    foreach (var file in image.GetFiles(image.Root.FullName, "*.*", SearchOption.AllDirectories))
                    {
                        Stream? stream = null;
                        try
                        {
                            var info = image.GetFileInfo(file);
                            stream = info.OpenRead();
                            governor.CheckResourceGovernor(info.Length);
                        }
                        catch (Exception e)
                        {
                            Logger.Debug("Error reading {0} from WIM {1} ({2}:{3})", file, image.FriendlyName, e.GetType(), e.Message);
                        }
                        if (stream != null)
                        {
                            var newFileEntry = new FileEntry($"{image.FriendlyName}\\{file}", stream, fileEntry);
                            foreach (var entry in Context.ExtractFile(newFileEntry, options, governor))
                            {
                                yield return entry;
                            }
                            stream.Dispose();
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
