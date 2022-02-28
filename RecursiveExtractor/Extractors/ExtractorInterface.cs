using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// The interface for an extractor that supports Synchronous Extraction.
    /// </summary>
    public interface ExtractorInterface
    {
        /// <summary>
        /// Extract <see cref="FileEntry"/> from the provided <see cref="FileEntry"/>
        /// </summary>
        /// <param name="fileEntry">The <see cref="FileEntry"/> to extract.</param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns><see cref="IAsyncEnumerable{FileEntry}"/> of the files contained in the <paramref name="fileEntry"/> provided.</returns>
        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true);
    }
}