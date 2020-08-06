using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public abstract class ExtractorImplementation
    {
        public abstract IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor);
        public ArchiveFileType TargetType = ArchiveFileType.INVALID;
        internal const int BUFFER_SIZE = 32768;
    }
}