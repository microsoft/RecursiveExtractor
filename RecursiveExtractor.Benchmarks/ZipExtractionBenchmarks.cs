// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Measures how much time RecursiveExtractor adds on top of the underlying archive library
    /// for a plain zip, and isolates the cost of the pieces that only RecursiveExtractor does
    /// (quine detection, per-entry type detection, per-entry stream buffering).
    /// </summary>
    [MemoryDiagnoser]
    public class ZipExtractionBenchmarks
    {
        private readonly Extractor _extractor = new();
        private readonly ExtractorOptions _options = new();
        private readonly ExtractorOptions _noRecurseOptions = new() { Recurse = false };

        private byte[] _archive = Array.Empty<byte>();
        private byte[] _copyBuffer = Array.Empty<byte>();
        private FileEntry? _retainedRoot;
        private List<FileEntry> _retainedEntries = new();

        /// <summary>
        /// Number of entries in the benchmark archive.
        /// </summary>
        [Params(100, 1000)]
        public int EntryCount { get; set; }

        /// <summary>
        /// Size in bytes of each entry in the benchmark archive.
        /// </summary>
        [Params(4096)]
        public int EntrySize { get; set; }

        /// <summary>
        /// Builds the archive under test and materializes one extraction so the quine check can be
        /// timed in isolation against exactly the same entries.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _archive = SyntheticArchives.Create(BenchmarkArchiveKind.ZipDeflate, EntryCount, EntrySize);
            _copyBuffer = new byte[81920];

            // Kept alive for the whole run: AreIdentical needs the parent's stream to still be readable.
            _retainedRoot = new FileEntry("benchmark.zip", new MemoryStream(_archive, false), passthroughStream: true);
            _retainedEntries = new List<FileEntry>(EntryCount);
            foreach (var entry in _extractor.Extract(_retainedRoot, _options))
            {
                _retainedEntries.Add(entry);
            }
        }

        /// <summary>
        /// Releases the entries retained for the quine benchmark.
        /// </summary>
        [GlobalCleanup]
        public void Cleanup()
        {
            foreach (var entry in _retainedEntries)
            {
                entry.Content.Dispose();
            }

            _retainedEntries.Clear();
            _retainedRoot?.Content.Dispose();
        }

        /// <summary>
        /// The floor: SharpCompress decompressing every entry, discarding the bytes.
        /// </summary>
        /// <returns>Bytes decompressed.</returns>
        [Benchmark(Baseline = true, Description = "SharpCompress: decompress only")]
        public long SharpCompressDecompressOnly()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            using var archive = ZipArchive.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                using var entryStream = entry.OpenEntryStream();
                total += entryStream.CopyToCounting(Stream.Null, _copyBuffer);
            }

            return total;
        }

        /// <summary>
        /// SharpCompress decompressing every entry into a seekable MemoryStream, which is what
        /// RecursiveExtractor has to do to hand back a seekable <see cref="FileEntry.Content"/>.
        /// </summary>
        /// <returns>Bytes buffered.</returns>
        [Benchmark(Description = "SharpCompress: decompress + buffer")]
        public long SharpCompressWithBuffering()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            using var archive = ZipArchive.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                using var entryStream = entry.OpenEntryStream();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                total += buffer.Length;
            }

            return total;
        }

        /// <summary>
        /// RecursiveExtractor, synchronous, recursing into entries (the default).
        /// </summary>
        /// <returns>Bytes extracted.</returns>
        [Benchmark(Description = "RE: Extract (recurse)")]
        public long RecursiveExtractorExtract()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            var root = new FileEntry("benchmark.zip", source, passthroughStream: true);
            foreach (var entry in _extractor.Extract(root, _options))
            {
                total += entry.Content.Length;
                entry.Content.Dispose();
            }

            return total;
        }

        /// <summary>
        /// RecursiveExtractor with recursion disabled. The difference against
        /// <see cref="RecursiveExtractorExtract"/> is the per-entry recursion overhead:
        /// resource governor accounting, quine detection and archive type detection.
        /// </summary>
        /// <returns>Bytes extracted.</returns>
        [Benchmark(Description = "RE: Extract (no recurse)")]
        public long RecursiveExtractorExtractNoRecurse()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            var root = new FileEntry("benchmark.zip", source, passthroughStream: true);
            foreach (var entry in _extractor.Extract(root, _noRecurseOptions))
            {
                total += entry.Content.Length;
                entry.Content.Dispose();
            }

            return total;
        }

        /// <summary>
        /// RecursiveExtractor, asynchronous.
        /// </summary>
        /// <returns>Bytes extracted.</returns>
        [Benchmark(Description = "RE: ExtractAsync (recurse)")]
        public async Task<long> RecursiveExtractorExtractAsync()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            var root = new FileEntry("benchmark.zip", source, passthroughStream: true);
            await foreach (var entry in _extractor.ExtractAsync(root, _options))
            {
                total += entry.Content.Length;
                entry.Content.Dispose();
            }

            return total;
        }

        /// <summary>
        /// The convenience overload that takes a Stream. It builds its own <see cref="FileEntry"/>
        /// without passthrough, so the whole archive is copied into a new backing stream first.
        /// </summary>
        /// <returns>Bytes extracted.</returns>
        [Benchmark(Description = "RE: Extract(name, Stream) overload")]
        public long RecursiveExtractorExtractStreamOverload()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            foreach (var entry in _extractor.Extract("benchmark.zip", source, _options))
            {
                total += entry.Content.Length;
                entry.Content.Dispose();
            }

            return total;
        }

        /// <summary>
        /// The quine check alone, run over exactly the entries produced by extracting this archive.
        /// Compare against <see cref="RecursiveExtractorExtract"/> to see its share of total time.
        /// </summary>
        /// <returns>Number of quines found (always zero here).</returns>
        [Benchmark(Description = "RE: IsQuine over all entries only")]
        public int QuineCheckOnly()
        {
            var found = 0;
            for (var i = 0; i < _retainedEntries.Count; i++)
            {
                if (Extractor.IsQuine(_retainedEntries[i]))
                {
                    found++;
                }
            }

            return found;
        }

        /// <summary>
        /// Archive type detection alone, run over the same entries. This is what
        /// <see cref="FileEntry.ArchiveType"/> costs per entry during recursion.
        /// </summary>
        /// <returns>Number of entries detected as an archive.</returns>
        [Benchmark(Description = "RE: DetectFileType over all entries only")]
        public int TypeDetectionOnly()
        {
            var archives = 0;
            for (var i = 0; i < _retainedEntries.Count; i++)
            {
                if (_retainedEntries[i].ArchiveType != ArchiveFileType.UNKNOWN)
                {
                    archives++;
                }
            }

            return archives;
        }
    }
}
