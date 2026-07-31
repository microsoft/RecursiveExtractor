// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Measures <see cref="MiniMagic.DetectFileType(Stream)"/>, which runs once per entry during
    /// recursion via <see cref="FileEntry.ArchiveType"/>. It seeks to the end of the stream for the
    /// DMG footer check, so the backing store matters.
    /// </summary>
    [MemoryDiagnoser]
    public class FileTypeDetectionBenchmarks
    {
        private MemoryStream? _plainMemory;
        private MemoryStream? _zipMemory;
        private FileStream? _plainFile;
        private string _tempFile = string.Empty;

        /// <summary>
        /// Size in bytes of the content being sniffed.
        /// </summary>
        [Params(4096, 1048576)]
        public int ContentSize { get; set; }

        /// <summary>
        /// Creates the streams under test.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _plainMemory = new MemoryStream(SyntheticArchives.CreatePayload(ContentSize, true), false);
            _zipMemory = new MemoryStream(SyntheticArchives.Create(BenchmarkArchiveKind.ZipDeflate, 4, 1024), false);

            _tempFile = Path.Combine(Path.GetTempPath(), $"re-bench-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(_tempFile, SyntheticArchives.CreatePayload(ContentSize, true));
            _plainFile = new FileStream(_tempFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// Disposes the streams and removes the temporary file.
        /// </summary>
        [GlobalCleanup]
        public void Cleanup()
        {
            _plainMemory?.Dispose();
            _zipMemory?.Dispose();
            _plainFile?.Dispose();

            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        /// <summary>
        /// Detection against an in-memory non-archive, the common case for leaf files.
        /// </summary>
        /// <returns>The detected type.</returns>
        [Benchmark(Baseline = true, Description = "MemoryStream, not an archive")]
        public ArchiveFileType MemoryStreamPlain() => MiniMagic.DetectFileType(_plainMemory!);

        /// <summary>
        /// Detection against an in-memory zip, which matches on the first four bytes.
        /// </summary>
        /// <returns>The detected type.</returns>
        [Benchmark(Description = "MemoryStream, zip")]
        public ArchiveFileType MemoryStreamZip() => MiniMagic.DetectFileType(_zipMemory!);

        /// <summary>
        /// Detection against a FileStream, which is what backs entries larger than
        /// <see cref="ExtractorOptions.MemoryStreamCutoff"/>.
        /// </summary>
        /// <returns>The detected type.</returns>
        [Benchmark(Description = "FileStream, not an archive")]
        public ArchiveFileType FileStreamPlain() => MiniMagic.DetectFileType(_plainFile!);
    }
}
