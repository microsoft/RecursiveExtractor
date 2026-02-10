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
/// See: https://github.com/microsoft/RecursiveExtractor/issues/new (steganography in ZIP).
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
            ExtractNonIndexedZipEntries = false,
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
            ExtractNonIndexedZipEntries = true,
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
            ExtractNonIndexedZipEntries = true,
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
            ExtractNonIndexedZipEntries = true,
            Recurse = false,
        }).ToList();

        Assert.True(results.Count > 0, "Expected at least one entry from TestData.zip");
        Assert.DoesNotContain(results, r => r.EntryStatus == FileEntryStatus.NonIndexedEntry);
    }
}
