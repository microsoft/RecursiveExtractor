// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Isolates the cost of <see cref="Extractor.IsQuine"/> for one entry. The check walks the
    /// parent chain, so cost scales with nesting depth rather than with the number of files in the
    /// archive, and <see cref="Extractor.AreIdentical"/> only reads content when an ancestor has
    /// both the same length and the same name.
    /// </summary>
    [MemoryDiagnoser]
    public class QuineDetectionBenchmarks
    {
        private readonly List<byte[]> _buffers = new();

        private FileEntry? _differentSizes;
        private FileEntry? _sameSizeDifferentNames;
        private FileEntry? _sameSizeSameName;
        private FileEntry? _actualQuine;

        /// <summary>
        /// Size in bytes of the content at each level of the chain.
        /// </summary>
        [Params(4096, 1048576)]
        public int ContentSize { get; set; }

        /// <summary>
        /// How many ancestors the entry being checked has.
        /// </summary>
        [Params(1, 4)]
        public int Depth { get; set; }

        /// <summary>
        /// Builds the parent chains under test.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            // Realistic case: an extracted file is never the same size as the archive it came from.
            _differentSizes = BuildChain(
                level => $"entry{level}.bin",
                level => Track(SyntheticArchives.CreatePayload(ContentSize + (level * 512), true)));

            // Uniform payloads: lengths collide, but names still short circuit the content compare.
            _sameSizeDifferentNames = BuildChain(
                level => $"entry{level}.bin",
                _ => Track(SyntheticArchives.CreatePayload(ContentSize, true)));

            // Worst case: same length and same name at every level forces a full byte compare
            // that only fails on the final byte.
            _sameSizeSameName = BuildChain(
                _ => "entry.bin",
                level =>
                {
                    var payload = SyntheticArchives.CreatePayload(ContentSize, true);
                    payload[payload.Length - 1] = (byte)level;
                    return Track(payload);
                });

            // A genuine quine: identical name and content, detected on the first comparison.
            var quinePayload = Track(SyntheticArchives.CreatePayload(ContentSize, true));
            _actualQuine = BuildChain(_ => "entry.bin", _ => quinePayload);
        }

        /// <summary>
        /// Releases the streams held by the chains.
        /// </summary>
        [GlobalCleanup]
        public void Cleanup()
        {
            _buffers.Clear();
            _differentSizes = null;
            _sameSizeDifferentNames = null;
            _sameSizeSameName = null;
            _actualQuine = null;
        }

        /// <summary>
        /// The path taken by nearly every real entry: content lengths differ from the ancestors.
        /// </summary>
        /// <returns>Whether a quine was detected.</returns>
        [Benchmark(Baseline = true, Description = "Ancestors differ in length (typical)")]
        public bool DifferentSizes() => Extractor.IsQuine(_differentSizes!);

        /// <summary>
        /// Lengths collide but names differ, so still no content comparison.
        /// </summary>
        /// <returns>Whether a quine was detected.</returns>
        [Benchmark(Description = "Same length, different name")]
        public bool SameSizeDifferentNames() => Extractor.IsQuine(_sameSizeDifferentNames!);

        /// <summary>
        /// Same length and name at every level, so every ancestor is compared byte for byte.
        /// </summary>
        /// <returns>Whether a quine was detected.</returns>
        [Benchmark(Description = "Same length + name (full compare, worst case)")]
        public bool SameSizeSameName() => Extractor.IsQuine(_sameSizeSameName!);

        /// <summary>
        /// An actual quine, which is detected on the first ancestor comparison.
        /// </summary>
        /// <returns>Whether a quine was detected.</returns>
        [Benchmark(Description = "Actual quine (detected)")]
        public bool ActualQuine() => Extractor.IsQuine(_actualQuine!);

        private FileEntry BuildChain(Func<int, string> nameFor, Func<int, byte[]> contentFor)
        {
            FileEntry? parent = null;
            FileEntry current = null!;

            for (var level = 0; level <= Depth; level++)
            {
                current = new FileEntry(nameFor(level), new MemoryStream(contentFor(level), false), parent, passthroughStream: true);
                parent = current;
            }

            return current;
        }

        private byte[] Track(byte[] payload)
        {
            _buffers.Add(payload);
            return payload;
        }
    }
}
