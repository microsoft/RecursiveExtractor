using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// An interface for an extractor that supports Asynchronous extraction.
    /// </summary>
    public interface AsyncExtractorInterface : ExtractorInterface
    {
        /// <summary>
        /// Extract <see cref="FileEntry"/> from the provided <see cref="FileEntry"/>
        /// </summary>
        /// <param name="fileEntry">The <see cref="FileEntry"/> to extract.</param>
        /// <param name="options">The <see cref="ExtractorOptions"/> to use for extraction.</param>
        /// <param name="governor">The <see cref="ResourceGovernor"/> to use for extraction.</param>
        /// <param name="topLevel">If this should be treated as the top level archive.</param>
        /// <returns><see cref="IAsyncEnumerable{FileEntry}"/> of the files contained in the <paramref name="fileEntry"/> provided.</returns>
        public IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true);
    }
}