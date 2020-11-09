using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// Holder of options for the Extractor.
    /// </summary>
    public class ExtractorOptions
    {
        /// <summary>
        /// Maximum number of bytes before using a FileStream
        /// </summary>
        public int MemoryStreamCutoff { get; set; } = 1024 * 1024;

        /// <summary>
        ///     Enable timing limit for processing.
        /// </summary>
        public bool EnableTiming { get; set; } = false;

        /// <summary>
        ///     If an archive cannot be extracted return a single file entry for the archive itself.
        /// </summary>
        public bool ExtractSelfOnFail { get; set; } = true;

        /// <summary>
        ///     The maximum number of bytes to extract from the archive and all embedded archives. Set to 0 to
        ///     remove limit. Note that MaxExpansionRatio may also apply. Defaults to 0.
        /// </summary>
        public long MaxExtractedBytes { get; set; } = 0;

        /// <summary>
        ///     By default, stop extracting if the total number of bytes seen is greater than this multiple of
        ///     the original archive size. Used to avoid denial of service (zip bombs and the like).
        /// </summary>
        public double MaxExtractedBytesRatio { get; set; } = 200.0;

        /// <summary>
        ///     If timing is enabled, stop processing after this time span. Used to avoid denial of service
        ///     (zip bombs and the like).
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(300);

        /// <summary>
        /// Batch size to use for parallel
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Run in parallel when possible
        /// </summary>
        public bool Parallel { get; set; }

        /// <summary>
        /// Parse these extensions as raw, don't traverse them.
        /// </summary>
        public IEnumerable<string>? RawExtensions { get; set; }

        /// <summary>
        /// Passwords to use
        /// </summary>
        public Dictionary<Regex, List<string>> Passwords { get; set; } = new Dictionary<Regex, List<string>>();
    }
}