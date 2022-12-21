using GlobExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// Holder of options for the Extractor.
    /// </summary>
    public class ExtractorOptions
    {
        private IEnumerable<string> _allowFilters = Array.Empty<string>();
        private IEnumerable<Glob> _allowGlobs = Array.Empty<Glob>();
        private IEnumerable<string> _denyFilters = Array.Empty<string>();
        private IEnumerable<Glob> _denyGlobs = Array.Empty<Glob>();

        /// <summary>
        /// Maximum number of bytes before using a FileStream. Default 100MB
        /// </summary>
        public int MemoryStreamCutoff { get; set; } = 1024 * 1024 * 100;

        /// <summary>
        ///     Enable timing limit for processing.
        /// </summary>
        public bool EnableTiming { get; set; }

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
        public IEnumerable<string> RawExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Passwords to use
        /// </summary>
        public Dictionary<Regex, List<string>> Passwords { get; set; } = new Dictionary<Regex, List<string>>();

        /// <summary>
        /// Should extraction recurse into archives
        /// </summary>
        public bool Recurse { get; set; } = true;

        /// <summary>
        /// Buffer size to use for FileStream backed FileEntries.
        /// </summary>
        public int FileStreamBufferSize { get; set; } = 4096;

        /// <summary>
        /// If set, only return files that match these glob filters
        /// </summary>
        public IEnumerable<string> AllowFilters
        {
            get
            {
                return _allowFilters;
            }
            set
            {
                _allowFilters = value;
                _allowGlobs = value.Select(x => new Glob(x));
            }
        }

        /// <summary>
        /// If set, don't return any files that match these glob filters
        /// </summary>
        public IEnumerable<string> DenyFilters
        {
            get
            {
                return _denyFilters;
            }
            set
            {
                _denyFilters = value;
                _denyGlobs = value.Select(x => new Glob(x));
            }
        }

        /// <summary>
        /// Allow only the specified archive types to be extracted
        /// </summary>
        public IEnumerable<ArchiveFileType> AllowTypes { get; set; } = Array.Empty<ArchiveFileType>();

        /// <summary>
        /// Prevent the specified archives types from being extracted
        /// </summary>
        public IEnumerable<ArchiveFileType> DenyTypes { get; set; } = Array.Empty<ArchiveFileType>();

        /// <summary>
        /// Always expect the TopLevel to be extracted to be an archive.
        /// </summary>
        public bool RequireTopLevelToBeArchive { get; set; } = false;

        /// <summary>
        /// If the file name provided should be extracted given the filter arguments in this ExtractorOptions instance
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool FileNamePasses(string filename) => (!_denyGlobs.Any() || (_denyGlobs.Any() && !_denyGlobs.Any(x => x.IsMatch(filename)))) &&
                                                        (!_allowGlobs.Any() ||
                                                         _allowGlobs.Any(x => x.IsMatch(filename)));

        /// <summary>
        /// Checks if the given <see cref="ArchiveFileType"/> is allowable by the set <see cref="DenyTypes"/> and <see cref="AllowTypes"/>.
        /// </summary>
        /// <param name="type">The <see cref="ArchiveFileType"/> to check.</param>
        /// <returns>True if the options allow for extracting the specified type.</returns>
        public bool IsAcceptableType(ArchiveFileType type) => !DenyTypes.Contains(type) && (!AllowTypes.Any() || AllowTypes.Contains(type));
    }
}