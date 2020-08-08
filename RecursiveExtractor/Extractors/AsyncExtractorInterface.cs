using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    public interface AsyncExtractorInterface : ExtractorInterface
    {
        public IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor);
    }
}