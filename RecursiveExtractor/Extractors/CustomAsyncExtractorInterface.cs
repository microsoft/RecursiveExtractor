using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Extractors
{
    /// <summary>
    /// An interface for custom extractors that can determine if they can handle a given stream.
    /// This allows library users to extend the extractor with support for additional archive types.
    /// </summary>
    public interface CustomAsyncExtractorInterface : AsyncExtractorInterface
    {
        /// <summary>
        /// Determines if this extractor can extract the given stream based on binary signatures or other criteria.
        /// This method should check the stream's content (similar to how MiniMagic works) and return true if this
        /// extractor supports the file format.
        /// </summary>
        /// <param name="stream">The stream to check. The implementation should preserve the stream's original position.</param>
        /// <returns>True if this extractor can handle the stream, false otherwise.</returns>
        bool CanExtract(Stream stream);
    }
}
