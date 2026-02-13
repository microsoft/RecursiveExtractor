// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class FileMetadataTests
{
    [Fact]
    public async Task TarEntries_HaveMetadata()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.tar");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.NotNull(entry.Metadata!.Mode);
            // Regular files in TestData.tar have mode 0644 (octal) = 420 (decimal)
            Assert.Equal(420, entry.Metadata.Mode);
            Assert.False(entry.Metadata.IsExecutable);
            Assert.False(entry.Metadata.IsSetUid);
            Assert.False(entry.Metadata.IsSetGid);
            Assert.NotNull(entry.Metadata.Uid);
            Assert.NotNull(entry.Metadata.Gid);
        }
    }

    [Fact]
    public void TarEntries_HaveMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.tar");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.NotNull(entry.Metadata!.Mode);
            Assert.Equal(420, entry.Metadata.Mode);
            Assert.False(entry.Metadata.IsExecutable);
            Assert.NotNull(entry.Metadata.Uid);
            Assert.NotNull(entry.Metadata.Gid);
        }
    }

    [Fact]
    public async Task ArEntries_HaveMetadata()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.a");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.NotNull(entry.Metadata!.Mode);
            // ar files in TestData.a have mode 0644 (octal) = 420 (decimal)
            Assert.Equal(420, entry.Metadata.Mode);
            Assert.False(entry.Metadata.IsExecutable);
            Assert.NotNull(entry.Metadata.Uid);
            Assert.Equal(0L, entry.Metadata.Uid);
            Assert.NotNull(entry.Metadata.Gid);
            Assert.Equal(0L, entry.Metadata.Gid);
        }
    }

    [Fact]
    public void ArEntries_HaveMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.a");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.NotNull(entry.Metadata!.Mode);
            Assert.Equal(420, entry.Metadata.Mode);
            Assert.NotNull(entry.Metadata.Uid);
            Assert.NotNull(entry.Metadata.Gid);
        }
    }

    [Fact]
    public void MetadataDefaults_AreNull()
    {
        var metadata = new FileEntryMetadata();
        Assert.Null(metadata.Mode);
        Assert.Null(metadata.Uid);
        Assert.Null(metadata.Gid);
        Assert.Null(metadata.IsExecutable);
        Assert.Null(metadata.IsSetUid);
        Assert.Null(metadata.IsSetGid);
    }

    [Fact]
    public void IsExecutable_DerivedFromMode()
    {
        // 0755 (octal) = 493 (decimal)
        var metadata = new FileEntryMetadata { Mode = 493 };
        Assert.True(metadata.IsExecutable);
        Assert.False(metadata.IsSetUid);
        Assert.False(metadata.IsSetGid);

        // 0644 (octal) = 420 (decimal)
        metadata = new FileEntryMetadata { Mode = 420 };
        Assert.False(metadata.IsExecutable);
    }

    [Fact]
    public void SetUidSetGid_DerivedFromMode()
    {
        // 04755 (octal) = 2541 (decimal) — setuid + rwxr-xr-x
        var metadata = new FileEntryMetadata { Mode = 2541 };
        Assert.True(metadata.IsSetUid);
        Assert.False(metadata.IsSetGid);
        Assert.True(metadata.IsExecutable);

        // 02755 (octal) = 1517 (decimal) — setgid + rwxr-xr-x
        metadata = new FileEntryMetadata { Mode = 1517 };
        Assert.False(metadata.IsSetUid);
        Assert.True(metadata.IsSetGid);
        Assert.True(metadata.IsExecutable);
    }

    [Fact]
    public void FileEntry_MetadataDefaultsToNull()
    {
        using var stream = new MemoryStream(new byte[] { 0 });
        var entry = new FileEntry("test.txt", stream);
        Assert.Null(entry.Metadata);
    }

    [Fact]
    public async Task IsoEntries_MetadataIsNullWithoutRockRidge()
    {
        // TestData.iso does not have RockRidge extensions, so Unix metadata is not available
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.iso");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            // Without RockRidge extensions, metadata should be null
            Assert.Null(entry.Metadata);
        }
    }

    [Fact]
    public void IsoEntries_MetadataIsNullWithoutRockRidge_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.iso");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.Null(entry.Metadata);
        }
    }

    [Fact]
    public async Task IsoRockRidgeEntries_HaveMetadata()
    {
        // TestDataRockRidge.iso has RockRidge extensions with Unix permissions
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestDataRockRidge.iso");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.NotNull(entry.Metadata!.Mode);
            Assert.NotNull(entry.Metadata.Uid);
            Assert.NotNull(entry.Metadata.Gid);
        }
    }

    [Fact]
    public void IsoRockRidgeEntries_HaveMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestDataRockRidge.iso");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.NotNull(entry.Metadata!.Mode);
            Assert.NotNull(entry.Metadata.Uid);
            Assert.NotNull(entry.Metadata.Gid);
        }
    }
}
