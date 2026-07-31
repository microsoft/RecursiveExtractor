// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Measures writing an extracted archive to disk with and without
    /// <see cref="ExtractorOptions.Parallel"/>. Only the write side is parallelized; the
    /// extraction enumeration itself is sequential because entries are produced by a single
    /// forward pass over the source archive.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 1, iterationCount: 5)]
    public class ExtractToDirectoryBenchmarks
    {
        private readonly Extractor _extractor = new();

        private byte[] _archive = Array.Empty<byte>();
        private string _outputRoot = string.Empty;
        private string _outputDirectory = string.Empty;

        /// <summary>
        /// Whether entries are written to disk in parallel.
        /// </summary>
        [Params(false, true)]
        public bool Parallel { get; set; }

        /// <summary>
        /// Number of entries in the archive being written out.
        /// </summary>
        [Params(500)]
        public int EntryCount { get; set; }

        /// <summary>
        /// Builds the archive and the root output folder.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _archive = SyntheticArchives.Create(BenchmarkArchiveKind.ZipDeflate, EntryCount, 8192);
            _outputRoot = Path.Combine(Path.GetTempPath(), $"re-bench-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_outputRoot);
        }

        /// <summary>
        /// Removes the root output folder.
        /// </summary>
        [GlobalCleanup]
        public void Cleanup() => TryDelete(_outputRoot);

        /// <summary>
        /// Gives each iteration a clean output folder.
        /// </summary>
        [IterationSetup]
        public void IterationSetup()
        {
            _outputDirectory = Path.Combine(_outputRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>
        /// Deletes the folder written by the previous iteration.
        /// </summary>
        [IterationCleanup]
        public void IterationCleanup() => TryDelete(_outputDirectory);

        /// <summary>
        /// Extracts the archive to disk.
        /// </summary>
        /// <returns>The extraction status.</returns>
        [Benchmark(Description = "ExtractToDirectory")]
        public ExtractionStatusCode ExtractToDirectory()
        {
            using var source = new MemoryStream(_archive, false);
            var root = new FileEntry("benchmark.zip", source, passthroughStream: true);
            return _extractor.ExtractToDirectory(_outputDirectory, root, new ExtractorOptions { Parallel = Parallel });
        }

        private static void TryDelete(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (IOException)
            {
                // Best effort cleanup of temporary output.
            }
        }
    }
}
