using Microsoft.CST.RecursiveExtractor;
using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace RecursiveExtractor.Tests;

/// <summary>
/// Guards against re-introducing the per-entry decoder leak, where an extractor passed
/// <c>entry.OpenEntryStream()</c> into a <see cref="FileEntry"/> without disposing it.
/// </summary>
/// <remarks>
/// 7-Zip is solid, so SharpCompress builds a fresh LZMA decoder chain for every entry stream that
/// is opened. Each chain holds a 1 MiB dictionary window plus a 128 KiB read cache, and both are
/// only released when the entry stream is disposed. Leaving them open therefore costs roughly
/// 1.1 MiB per entry and pushes the dictionary onto the large object heap.
/// </remarks>
public class EntryStreamDisposalTests
{
    private const int EntryCount = 40;
    private const int EntrySize = 8192;

    /// <summary>
    /// Comfortably above what extraction actually allocates per entry (~56 KiB) and comfortably
    /// below the ~1.1 MiB per entry that leaking a decoder chain would cost.
    /// </summary>
    private const long MaxBytesAllocatedPerEntry = 300 * 1024;

    private static byte[] CreateSevenZip()
    {
        using var output = new MemoryStream();
        using (var writer = WriterFactory.OpenWriter(output, ArchiveType.SevenZip, new WriterOptions(CompressionType.LZMA) { LeaveStreamOpen = true }))
        {
            var random = new Random(1234);
            for (var i = 0; i < EntryCount; i++)
            {
                var payload = new byte[EntrySize];
                random.NextBytes(payload);
                using var entryStream = new MemoryStream(payload, false);
                writer.Write($"entry{i:D4}.bin", entryStream, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            }
        }

        return output.ToArray();
    }

    private static long Extract(byte[] archive)
    {
        long total = 0;
        var extractor = new Extractor();
        using var source = new MemoryStream(archive, false);
        var root = new FileEntry("test.7z", source, passthroughStream: true);
        foreach (var entry in extractor.Extract(root, new ExtractorOptions()))
        {
            total += entry.Content.Length;
            entry.Content.Dispose();
        }

        return total;
    }

    [Fact]
    public void SevenZipExtractionDoesNotRetainADecoderPerEntry()
    {
        var archive = CreateSevenZip();

        // Warm up so JIT and one time initialisation are not attributed to the measured run.
        Assert.Equal(EntryCount * (long)EntrySize, Extract(archive));

#if !NETFRAMEWORK
        // Per thread rather than per process, so tests running in parallel cannot skew the result.
        // Extract is synchronous, so everything it allocates lands on this thread.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var extracted = Extract(archive);
        var allocatedPerEntry = (GC.GetAllocatedBytesForCurrentThread() - before) / EntryCount;

        Assert.Equal(EntryCount * (long)EntrySize, extracted);
        Assert.True(
            allocatedPerEntry < MaxBytesAllocatedPerEntry,
            $"Extraction allocated {allocatedPerEntry} bytes per entry, over the {MaxBytesAllocatedPerEntry} byte budget. " +
            "This usually means an entry stream is no longer being disposed, leaking a decoder and its dictionary per entry.");
#endif
    }

    [Fact]
    public void SevenZipExtractionStillReturnsEveryEntryIntact()
    {
        var archive = CreateSevenZip();
        var extractor = new Extractor();
        using var source = new MemoryStream(archive, false);
        var root = new FileEntry("test.7z", source, passthroughStream: true);

        var entries = extractor.Extract(root, new ExtractorOptions()).ToList();

        Assert.Equal(EntryCount, entries.Count);
        foreach (var entry in entries)
        {
            Assert.Equal(EntrySize, entry.Content.Length);
            entry.Content.Dispose();
        }
    }
}
