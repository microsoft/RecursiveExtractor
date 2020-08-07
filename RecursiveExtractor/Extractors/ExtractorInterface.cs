using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public interface ExtractorInterface
    {
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor);
    }
}