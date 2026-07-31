// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// The kinds of archives the benchmarks build. Keeping this separate from
    /// <see cref="ArchiveType"/> lets a single value describe both the container and the
    /// compression applied to it.
    /// </summary>
    public enum BenchmarkArchiveKind
    {
        /// <summary>Zip container, deflate compressed entries.</summary>
        ZipDeflate,

        /// <summary>Zip container, stored (uncompressed) entries.</summary>
        ZipStore,

        /// <summary>Uncompressed tar container.</summary>
        Tar,

        /// <summary>Tar container wrapped in gzip.</summary>
        TarGz,

        /// <summary>Tar container wrapped in bzip2.</summary>
        TarBz2,

        /// <summary>7-Zip container using LZMA.</summary>
        SevenZipLzma
    }

    /// <summary>
    /// Builds deterministic in-memory archives so the benchmarks do not depend on the test
    /// project's assets and so payload size, entry count and compressibility can be varied.
    /// </summary>
    public static class SyntheticArchives
    {
        private const int PayloadSeed = 0x5EED;

        private static readonly string[] Words =
        {
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
            "sed", "eiusmod", "tempor", "incididunt", "labore", "magna", "aliqua", "enim",
            "minim", "veniam", "quis", "nostrud", "exercitation", "ullamco", "laboris",
            "aliquip", "commodo", "consequat", "duis", "aute", "irure", "reprehenderit"
        };

        /// <summary>
        /// Creates a payload buffer of the requested size.
        /// </summary>
        /// <param name="size">Number of bytes to produce.</param>
        /// <param name="compressible">
        /// When true the payload is prose-like text that compressors shrink by roughly 2-3x, which
        /// is representative of real content. When false it is pseudo random data that compressors
        /// cannot shrink.
        /// </param>
        /// <param name="variant">
        /// Selects the pseudo random stream. The same variant always produces the same bytes, and
        /// different variants produce content with no cross-entry redundancy. That matters because
        /// repeating one buffer across every entry lets an outer archive layer compress the inner
        /// archive enormously, which trips <see cref="ExtractorOptions.MaxExtractedBytesRatio"/>.
        /// </param>
        /// <returns>The payload buffer.</returns>
        public static byte[] CreatePayload(int size, bool compressible, int variant = 0)
        {
            var bytes = new byte[size];
            var random = new Random(PayloadSeed + variant);

            if (compressible)
            {
                var written = 0;
                while (written < size)
                {
                    var word = Words[random.Next(Words.Length)];
                    for (var i = 0; i < word.Length && written < size; i++)
                    {
                        bytes[written++] = (byte)word[i];
                    }

                    if (written < size)
                    {
                        bytes[written++] = (byte)' ';
                    }
                }
            }
            else
            {
                random.NextBytes(bytes);
            }

            return bytes;
        }

        /// <summary>
        /// Builds an archive containing <paramref name="entryCount"/> entries of
        /// <paramref name="entrySize"/> bytes each.
        /// </summary>
        /// <param name="kind">The archive container and compression to use.</param>
        /// <param name="entryCount">Number of entries to write.</param>
        /// <param name="entrySize">Size in bytes of each entry.</param>
        /// <param name="compressible">Whether the entry payload should be compressible.</param>
        /// <returns>The bytes of the generated archive.</returns>
        public static byte[] Create(BenchmarkArchiveKind kind, int entryCount, int entrySize, bool compressible = true)
        {
            using var output = new MemoryStream();

            using (var writer = OpenWriter(kind, output))
            {
                for (var i = 0; i < entryCount; i++)
                {
                    // Distinct content per entry so no extractor can dedupe, and so an outer
                    // archive layer cannot collapse the inner one into a zip bomb ratio.
                    var payload = CreatePayload(entrySize, compressible, i);
                    using var entryStream = new MemoryStream(payload, false);
                    writer.Write($"dir{i % 8}/entry{i:D5}.bin", entryStream, DateTime.UnixEpoch);
                }
            }

            return output.ToArray();
        }

        /// <summary>
        /// Builds a zip that nests another zip <paramref name="depth"/> levels deep, with the
        /// innermost archive holding the requested entries. Used to measure the cost of recursion
        /// and of the quine check walking the parent chain.
        /// </summary>
        /// <param name="depth">How many wrapping zip layers to add around the leaf archive.</param>
        /// <param name="leafEntryCount">Number of entries in the innermost archive.</param>
        /// <param name="leafEntrySize">Size in bytes of each innermost entry.</param>
        /// <returns>The bytes of the generated archive.</returns>
        public static byte[] CreateNestedZip(int depth, int leafEntryCount, int leafEntrySize)
        {
            // Incompressible leaves keep the expansion ratio well under
            // ExtractorOptions.MaxExtractedBytesRatio, so nesting does not trip the zip bomb guard.
            var current = Create(BenchmarkArchiveKind.ZipDeflate, leafEntryCount, leafEntrySize, compressible: false);

            for (var level = 0; level < depth; level++)
            {
                using var output = new MemoryStream();
                using (var writer = OpenWriter(BenchmarkArchiveKind.ZipDeflate, output))
                {
                    using var entryStream = new MemoryStream(current, false);
                    writer.Write($"level{level}.zip", entryStream, DateTime.UnixEpoch);
                }

                current = output.ToArray();
            }

            return current;
        }

        /// <summary>
        /// The file name (and therefore the extension used for type detection) that matches the
        /// supplied <see cref="BenchmarkArchiveKind"/>.
        /// </summary>
        /// <param name="kind">The archive kind.</param>
        /// <returns>A representative file name.</returns>
        public static string FileNameFor(BenchmarkArchiveKind kind) => kind switch
        {
            BenchmarkArchiveKind.ZipDeflate => "benchmark.zip",
            BenchmarkArchiveKind.ZipStore => "benchmark.zip",
            BenchmarkArchiveKind.Tar => "benchmark.tar",
            BenchmarkArchiveKind.TarGz => "benchmark.tar.gz",
            BenchmarkArchiveKind.TarBz2 => "benchmark.tar.bz2",
            BenchmarkArchiveKind.SevenZipLzma => "benchmark.7z",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private static IWriter OpenWriter(BenchmarkArchiveKind kind, Stream output) => kind switch
        {
            // Tar is written directly rather than through WriterFactory so the ustar magic is
            // emitted; MiniMagic looks for it at offset 257 and the older v7 header has no magic.
            BenchmarkArchiveKind.Tar => new TarWriter(output, TarOptions(CompressionType.None)),
            BenchmarkArchiveKind.TarGz => new TarWriter(output, TarOptions(CompressionType.GZip)),
            BenchmarkArchiveKind.TarBz2 => new TarWriter(output, TarOptions(CompressionType.BZip2)),
            BenchmarkArchiveKind.ZipDeflate => WriterFactory.OpenWriter(output, ArchiveType.Zip, ZipOptions(CompressionType.Deflate)),
            BenchmarkArchiveKind.ZipStore => WriterFactory.OpenWriter(output, ArchiveType.Zip, ZipOptions(CompressionType.None)),
            BenchmarkArchiveKind.SevenZipLzma => WriterFactory.OpenWriter(output, ArchiveType.SevenZip, ZipOptions(CompressionType.LZMA)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private static WriterOptions ZipOptions(CompressionType compressionType) =>
            new(compressionType) { LeaveStreamOpen = true };

        private static TarWriterOptions TarOptions(CompressionType compressionType) =>
            // CompressionLevel defaults to 0 (stored) on TarWriterOptions, which would make
            // tar.gz identical in size to plain tar and misrepresent the decompression cost.
            new(compressionType, true, TarHeaderWriteFormat.USTAR) { LeaveStreamOpen = true, CompressionLevel = 6 };

    }
}
