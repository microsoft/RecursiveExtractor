// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using SharpCompress.Archives;
using SharpCompress.Readers;
using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Extracts the same payload from every supported container so the cost attributable to the
    /// underlying format implementation can be separated from RecursiveExtractor's own overhead.
    /// </summary>
    [MemoryDiagnoser]
    public class ArchiveFormatBenchmarks
    {
        private readonly Extractor _extractor = new();
        private readonly ExtractorOptions _options = new();

        private byte[] _archive = Array.Empty<byte>();
        private byte[] _copyBuffer = Array.Empty<byte>();
        private string _fileName = string.Empty;

        /// <summary>
        /// The container and compression under test.
        /// </summary>
        [Params(
            BenchmarkArchiveKind.ZipStore,
            BenchmarkArchiveKind.ZipDeflate,
            BenchmarkArchiveKind.Tar,
            BenchmarkArchiveKind.TarGz,
            BenchmarkArchiveKind.TarBz2,
            BenchmarkArchiveKind.SevenZipLzma)]
        public BenchmarkArchiveKind Kind { get; set; }

        /// <summary>
        /// Builds the archive under test.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            _archive = SyntheticArchives.Create(Kind, EntryCount, EntrySize);
            _fileName = SyntheticArchives.FileNameFor(Kind);
            _copyBuffer = new byte[81920];
        }

        /// <summary>
        /// Number of entries in each generated archive.
        /// </summary>
        public const int EntryCount = 200;

        /// <summary>
        /// Size in bytes of each entry in each generated archive.
        /// </summary>
        public const int EntrySize = 8192;

        /// <summary>
        /// SharpCompress reading the archive and discarding the bytes. Forward-only readers handle
        /// the outer compression layer of the tar variants transparently, so they stay comparable
        /// with what RecursiveExtractor produces; 7z has no forward-only reader and is read through
        /// the random access archive API instead.
        /// </summary>
        /// <returns>Bytes decompressed.</returns>
        [Benchmark(Baseline = true, Description = "SharpCompress reader")]
        public long SharpCompressReader()
        {
            using var source = new MemoryStream(_archive, false);
            return Kind == BenchmarkArchiveKind.SevenZipLzma
                ? ReadWithArchiveApi(source)
                : ReadWithReaderApi(source);
        }

        private long ReadWithReaderApi(Stream source)
        {
            long total = 0;
            using var reader = ReaderFactory.OpenReader(source, new ReaderOptions { LeaveStreamOpen = true });
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                using var entryStream = reader.OpenEntryStream();
                total += entryStream.CopyToCounting(Stream.Null, _copyBuffer);
            }

            return total;
        }

        private long ReadWithArchiveApi(Stream source)
        {
            long total = 0;
            using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
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
        /// RecursiveExtractor extracting the same archive.
        /// </summary>
        /// <returns>Bytes extracted.</returns>
        [Benchmark(Description = "RecursiveExtractor.Extract")]
        public long RecursiveExtractorExtract()
        {
            long total = 0;
            using var source = new MemoryStream(_archive, false);
            var root = new FileEntry(_fileName, source, passthroughStream: true);
            foreach (var entry in _extractor.Extract(root, _options))
            {
                total += entry.Content.Length;
                entry.Content.Dispose();
            }

            return total;
        }
    }
}
