using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// This interface defines Extract
    /// </summary>
    public interface ExtractorInterface
    {
        /// <summary>
        /// Extract files from a FileEntry
        /// </summary>
        /// <param name="fileEntry">The FileEntry to extract from</param>
        /// <param name="options">The ExtractorOptions to use</param>
        /// <param name="governor">The ResourceGovernor to use</param>
        /// <returns></returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true);
    }
}