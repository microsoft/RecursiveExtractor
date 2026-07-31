using Microsoft.CST.RecursiveExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

/// <summary>
/// Tests that the ZIP extractor can discover and extract entries whose local file headers
/// exist in the stream but are absent from the central directory ("non-indexed" entries).
/// </summary>
public class NonIndexedZipEntryTests
{
    /// <summary>
    /// Builds a tampered ZIP whose central directory only references "visible.txt",
    /// but the raw stream also contains a local-header entry for "hidden.txt".
    /// </summary>
    private static byte[] CraftZipWithHiddenEntry()
    {
        // --- Create two independent, well-formed ZIPs ---

        byte[] zipWithVisible;
        using (var ms = new MemoryStream())
        {
            using (var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var ve = arc.CreateEntry("visible.txt", CompressionLevel.NoCompression);
                using var vw = new StreamWriter(ve.Open());
                vw.Write("VISIBLE_PAYLOAD");
            }
            zipWithVisible = ms.ToArray();
        }

        byte[] zipWithHidden;
        using (var ms = new MemoryStream())
        {
            using (var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var he = arc.CreateEntry("hidden.txt", CompressionLevel.NoCompression);
                using var hw = new StreamWriter(he.Open());
                hw.Write("SECRET_PAYLOAD");
            }
            zipWithHidden = ms.ToArray();
        }

        // --- Locate the End-Of-Central-Directory in each ---
        int eocdVisible = ScanBackwardsForEocd(zipWithVisible);
        int eocdHidden = ScanBackwardsForEocd(zipWithHidden);

        // Central directory offset is a uint32 at EOCD + 16
        uint cdOffsetVisible = BitConverter.ToUInt32(zipWithVisible, eocdVisible + 16);
        uint cdOffsetHidden = BitConverter.ToUInt32(zipWithHidden, eocdHidden + 16);

        // Everything before the CD in the hidden ZIP is the local-header + payload for "hidden.txt"
        byte[] hiddenLocalChunk = new byte[cdOffsetHidden];
        Array.Copy(zipWithHidden, 0, hiddenLocalChunk, 0, (int)cdOffsetHidden);

        // --- Assemble the tampered ZIP ---
        // Layout: [visible local entries] [hidden local entry] [visible CD + EOCD]
        // The central directory still only references "visible.txt".
        using var output = new MemoryStream();

        // 1) Local entries from the visible zip (everything before CD)
        output.Write(zipWithVisible, 0, (int)cdOffsetVisible);

        // 2) Splice in the hidden entry's local header + data
        output.Write(hiddenLocalChunk, 0, hiddenLocalChunk.Length);

        // 3) Copy the CD + EOCD from the visible zip, but patch the CD offset
        //    to account for the bytes we inserted.
        int cdAndEocdLength = zipWithVisible.Length - (int)cdOffsetVisible;
        byte[] cdAndEocd = new byte[cdAndEocdLength];
        Array.Copy(zipWithVisible, (int)cdOffsetVisible, cdAndEocd, 0, cdAndEocdLength);

        int eocdOffsetWithinTail = eocdVisible - (int)cdOffsetVisible;
        uint adjustedCdOffset = cdOffsetVisible + (uint)hiddenLocalChunk.Length;
        byte[] patchedOffsetBytes = BitConverter.GetBytes(adjustedCdOffset);
        patchedOffsetBytes.CopyTo(cdAndEocd, eocdOffsetWithinTail + 16);

        output.Write(cdAndEocd, 0, cdAndEocd.Length);
        return output.ToArray();
    }

    /// <summary>
    /// Scans backwards through <paramref name="zip"/> for the EOCD signature (PK\x05\x06).
    /// </summary>
    private static int ScanBackwardsForEocd(byte[] zip)
    {
        for (int i = zip.Length - 22; i >= 0; i--)
        {
            if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x05 && zip[i + 3] == 0x06)
                return i;
        }
        throw new InvalidDataException("ZIP EOCD record not found");
    }

    [Fact]
    public void SyncExtract_WithOptionOff_DoesNotReturnHiddenEntry()
    {
        var tamperedBytes = CraftZipWithHiddenEntry();
        var extractor = new Extractor();
        var fe = new FileEntry("tampered.zip", new MemoryStream(tamperedBytes), passthroughStream: true);

        var results = extractor.Extract(fe, new ExtractorOptions
        {
            ExtractNonIndexedEntries = false,
            Recurse = false,
        }).ToList();

        // Only the visible entry should appear
        Assert.Single(results);
        Assert.Equal("visible.txt", results[0].Name);
        Assert.Equal(FileEntryStatus.Default, results[0].EntryStatus);
    }

    [Fact]
    public void SyncExtract_WithOptionOn_ReturnsHiddenEntryFlagged()
    {
        var tamperedBytes = CraftZipWithHiddenEntry();
        var extractor = new Extractor();
        var fe = new FileEntry("tampered.zip", new MemoryStream(tamperedBytes), passthroughStream: true);

        var results = extractor.Extract(fe, new ExtractorOptions
        {
            ExtractNonIndexedEntries = true,
            Recurse = false,
        }).ToList();

        Assert.Equal(2, results.Count);

        var visibleResult = results.First(r => r.Name == "visible.txt");
        var hiddenResult = results.First(r => r.Name == "hidden.txt");

        Assert.Equal(FileEntryStatus.Default, visibleResult.EntryStatus);
        Assert.Equal(FileEntryStatus.NonIndexedEntry, hiddenResult.EntryStatus);

        // Verify the hidden entry has the expected content
        hiddenResult.Content.Position = 0;
        using var sr = new StreamReader(hiddenResult.Content);
        Assert.Equal("SECRET_PAYLOAD", sr.ReadToEnd());
    }

    [Fact]
    public async Task AsyncExtract_WithOptionOn_ReturnsHiddenEntryFlagged()
    {
        var tamperedBytes = CraftZipWithHiddenEntry();
        var extractor = new Extractor();
        var fe = new FileEntry("tampered.zip", new MemoryStream(tamperedBytes), passthroughStream: true);

        var results = new List<FileEntry>();
        await foreach (var entry in extractor.ExtractAsync(fe, new ExtractorOptions
        {
            ExtractNonIndexedEntries = true,
            Recurse = false,
        }))
        {
            results.Add(entry);
        }

        Assert.Equal(2, results.Count);

        var visibleResult = results.First(r => r.Name == "visible.txt");
        var hiddenResult = results.First(r => r.Name == "hidden.txt");

        Assert.Equal(FileEntryStatus.Default, visibleResult.EntryStatus);
        Assert.Equal(FileEntryStatus.NonIndexedEntry, hiddenResult.EntryStatus);

        hiddenResult.Content.Position = 0;
        using var sr = new StreamReader(hiddenResult.Content);
        Assert.Equal("SECRET_PAYLOAD", sr.ReadToEnd());
    }

    [Fact]
    public void NormalZip_WithOptionOn_ReturnsNoNonIndexedEntries()
    {
        // A clean ZIP should not produce any NonIndexedEntry results
        var extractor = new Extractor();
        var archivePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.zip");
        var results = extractor.Extract(archivePath, new ExtractorOptions
        {
            ExtractNonIndexedEntries = true,
            Recurse = false,
        }).ToList();

        Assert.True(results.Count > 0, "Expected at least one entry from TestData.zip");
        Assert.DoesNotContain(results, r => r.EntryStatus == FileEntryStatus.NonIndexedEntry);
    }

    /// <summary>
    /// Builds a tampered ZIP whose hidden entry mimics one written to a non-seekable stream: the
    /// local file header declares an uncompressed size of 0 and the real size would trail the
    /// payload in a data descriptor. The forward-only reader used by the non-indexed scan only
    /// ever sees that local header.
    /// </summary>
    private static byte[] CraftZipWithLargeStreamingHiddenEntry(int hiddenSize)
    {
        byte[] zipWithVisible;
        using (var ms = new MemoryStream())
        {
            using (var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var ve = arc.CreateEntry("visible.txt", CompressionLevel.NoCompression);
                using var vw = new StreamWriter(ve.Open());
                vw.Write("VISIBLE_PAYLOAD");
            }
            zipWithVisible = ms.ToArray();
        }

        byte[] zipWithHidden;
        using (var ms = new MemoryStream())
        {
            using (var arc = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var he = arc.CreateEntry("hidden.bin", CompressionLevel.Optimal);
                using var hs = he.Open();
                hs.Write(new byte[hiddenSize], 0, hiddenSize);
            }
            zipWithHidden = ms.ToArray();
        }

        // hidden.bin's local file header sits at offset 0; its uncompressed size field is at +22.
        Assert.True(zipWithHidden[0] == 0x50 && zipWithHidden[1] == 0x4B && zipWithHidden[2] == 0x03 && zipWithHidden[3] == 0x04,
            "Expected a local file header at the start of the hidden ZIP");
        Array.Clear(zipWithHidden, 22, 4);

        int eocdVisible = ScanBackwardsForEocd(zipWithVisible);
        int eocdHidden = ScanBackwardsForEocd(zipWithHidden);
        uint cdOffsetVisible = BitConverter.ToUInt32(zipWithVisible, eocdVisible + 16);
        uint cdOffsetHidden = BitConverter.ToUInt32(zipWithHidden, eocdHidden + 16);

        using var output = new MemoryStream();
        output.Write(zipWithVisible, 0, (int)cdOffsetVisible);
        output.Write(zipWithHidden, 0, (int)cdOffsetHidden);

        int tailLength = zipWithVisible.Length - (int)cdOffsetVisible;
        var tail = new byte[tailLength];
        Array.Copy(zipWithVisible, (int)cdOffsetVisible, tail, 0, tailLength);
        BitConverter.GetBytes(cdOffsetVisible + cdOffsetHidden).CopyTo(tail, eocdVisible - (int)cdOffsetVisible + 16);
        output.Write(tail, 0, tail.Length);

        return output.ToArray();
    }

    private static void AssertNotHeldEntirelyInMemory(Stream content)
    {
        if (content is SpillOverStream spillOver)
        {
            Assert.True(spillOver.HasSpilledToDisk, "Content exceeding the cutoff should have spilled to disk.");
            return;
        }

        Assert.IsType<FileStream>(content);
    }

    /// <summary>
    /// Regression guard: the non-indexed scan must not size its backing store from the declared
    /// entry size. A forward-only reader reports 0 for streaming entries, which would silently
    /// buffer arbitrarily large hidden content in memory regardless of MemoryStreamCutoff.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LargeStreamingHiddenEntry_RespectsMemoryStreamCutoff(bool useAsync)
    {
        const int hiddenSize = 4 * 1024 * 1024;
        var tamperedBytes = CraftZipWithLargeStreamingHiddenEntry(hiddenSize);

        var options = new ExtractorOptions
        {
            ExtractNonIndexedEntries = true,
            Recurse = false,
            MemoryStreamCutoff = 64 * 1024,
            // Isolate the backing store decision from the zip bomb guard.
            MaxExtractedBytesRatio = 0,
            MaxExtractedBytes = long.MaxValue,
        };

        var extractor = new Extractor();
        var fe = new FileEntry("tampered.zip", new MemoryStream(tamperedBytes), passthroughStream: true);

        var results = new List<FileEntry>();
        if (useAsync)
        {
            await foreach (var entry in extractor.ExtractAsync(fe, options))
            {
                results.Add(entry);
            }
        }
        else
        {
            results.AddRange(extractor.Extract(fe, options));
        }

        var hiddenResult = results.First(r => r.Name == "hidden.bin");
        Assert.Equal(FileEntryStatus.NonIndexedEntry, hiddenResult.EntryStatus);
        Assert.Equal(hiddenSize, hiddenResult.Content.Length);
        AssertNotHeldEntirelyInMemory(hiddenResult.Content);

        foreach (var result in results)
        {
            result.Content.Dispose();
        }
    }
}
