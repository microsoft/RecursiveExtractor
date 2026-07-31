// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Extracts zips nested inside zips to show how the whole pipeline scales with nesting depth.
    /// Every level re-buffers the inner archive and adds one more ancestor for the quine check to
    /// walk, so this is the shape most likely to expose recursion overhead.
    /// </summary>
    [MemoryDiagnoser]
    public class NestedArchiveBenchmarks
    {
        private readonly Extractor _extractor = new();
        private readonly ExtractorOptions _options = new();

        private byte[] _archive = Array.Empty<byte>();

        /// <summary>
        /// Number of zip layers wrapped around the innermost archive.
        /// </summary>
        [Params(0, 4, 8)]
        public int Depth { get; set; }

        /// <summary>
        /// Builds the nested archive under test.
        /// </summary>
        [GlobalSetup]
        public void Setup() => _archive = SyntheticArchives.CreateNestedZip(Depth, LeafEntryCount, LeafEntrySize);

        /// <summary>
        /// Number of entries in the innermost archive.
        /// </summary>
        public const int LeafEntryCount = 200;

        /// <summary>
        /// Size in bytes of each entry in the innermost archive.
        /// </summary>
        public const int LeafEntrySize = 4096;

        /// <summary>
        /// Extracts every leaf file out of the nested archive.
        /// </summary>
        /// <returns>Bytes extracted.</returns>
        [Benchmark(Description = "RecursiveExtractor.Extract")]
        public long RecursiveExtractorExtract()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            var root = new FileEntry("nested.zip", source, passthroughStream: true);
            foreach (var entry in _extractor.Extract(root, _options))
            {
                total += entry.Content.Length;
                entry.Content.Dispose();
            }

            return total;
        }
    }
}
