// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        Assert.Null(metadata.FileAttributes);
        Assert.Null(metadata.SecurityDescriptorSddl);
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
    public async Task IsoEntries_HaveDosAttributesButNoUnixMetadataWithoutRockRidge()
    {
        // TestData.iso has no RockRidge extensions, so Unix metadata is unavailable.
        // CDReader still implements IDosFileSystem, so file attributes are reported.
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.iso");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.Equal(FileAttributes.ReadOnly, entry.Metadata!.FileAttributes);
            Assert.Null(entry.Metadata.Mode);
            Assert.Null(entry.Metadata.Uid);
            Assert.Null(entry.Metadata.Gid);
            Assert.Null(entry.Metadata.SecurityDescriptorSddl);
        }
    }

    [Fact]
    public void IsoEntries_HaveDosAttributesButNoUnixMetadataWithoutRockRidge_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.iso");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.Equal(FileAttributes.ReadOnly, entry.Metadata!.FileAttributes);
            Assert.Null(entry.Metadata.Mode);
            Assert.Null(entry.Metadata.Uid);
            Assert.Null(entry.Metadata.Gid);
            Assert.Null(entry.Metadata.SecurityDescriptorSddl);
        }
    }

    [Fact]
    public async Task IsoRockRidgeEntries_HaveMetadata()
    {
        // TestDataRockRidge.iso has RockRidge extensions with Unix permissions
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestDataRockRidge.iso");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        AssertRockRidgeMetadata(results);
    }

    [Fact]
    public void IsoRockRidgeEntries_HaveMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestDataRockRidge.iso");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        AssertRockRidgeMetadata(results);
    }

    private static void AssertRockRidgeMetadata(IList<FileEntry> results)
    {
        Assert.Equal(2, results.Count);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.Equal(1001, entry.Metadata!.Uid);
            Assert.Equal(1001, entry.Metadata.Gid);
            Assert.NotNull(entry.Metadata.FileAttributes);
        }

        // testfile.txt is 0755 (493 decimal), subdir/nested.txt is 0644 (420 decimal)
        var topLevel = results.Single(x => x.Name == "testfile.txt");
        Assert.Equal(493, topLevel.Metadata!.Mode);
        Assert.True(topLevel.Metadata.IsExecutable);

        var nested = results.Single(x => x.Name == "nested.txt");
        Assert.Equal(420, nested.Metadata!.Mode);
        Assert.False(nested.Metadata.IsExecutable);
    }

    [Fact]
    public async Task IsoJolietRockRidgeEntries_HaveUnixMetadata()
    {
        // TestDataJolietRockRidge.iso carries both a Joliet supplementary volume descriptor and RockRidge
        // SUSP records. DiscUtils gives Joliet priority and only parses SUSP records for the variant it
        // activates, so the Unix metadata is only reachable through a second reader that skips Joliet.
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestDataJolietRockRidge.iso");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        AssertJolietRockRidgeMetadata(results);
    }

    [Fact]
    public void IsoJolietRockRidgeEntries_HaveUnixMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestDataJolietRockRidge.iso");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        AssertJolietRockRidgeMetadata(results);
    }

    private static void AssertJolietRockRidgeMetadata(IList<FileEntry> results)
    {
        Assert.Equal(2, results.Count);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.Equal(0, entry.Metadata!.Uid);
            Assert.Equal(0, entry.Metadata.Gid);
            // File attributes still come from the Joliet tree the entries were enumerated from
            Assert.Equal(FileAttributes.ReadOnly, entry.Metadata.FileAttributes);
            Assert.Null(entry.Metadata.SecurityDescriptorSddl);
        }

        // testfile.txt is 0755 (493 decimal), subdir/nested.txt is 0644 (420 decimal)
        var topLevel = results.Single(x => x.Name == "testfile.txt");
        Assert.Equal(493, topLevel.Metadata!.Mode);
        Assert.True(topLevel.Metadata.IsExecutable);

        var nested = results.Single(x => x.Name == "nested.txt");
        Assert.Equal(420, nested.Metadata!.Mode);
        Assert.False(nested.Metadata.IsExecutable);
    }

    [Fact]
    public async Task UdfEntries_HaveNoMetadata()
    {
        // UdfReader implements none of the Unix/DOS/Windows file system interfaces,
        // so no metadata is available for pure UDF images.
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "UdfTest.iso");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.Null(entry.Metadata);
        }
    }

    [Fact]
    public void UdfEntries_HaveNoMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "UdfTest.iso");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.Null(entry.Metadata);
        }
    }

    [Fact]
    public async Task VhdxNtfsEntries_HaveWindowsMetadata()
    {
        // TestData.vhdx contains an NTFS file system that implements IDosFileSystem and IWindowsFileSystem
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.vhdx");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        AssertNtfsMetadata(results);
    }

    [Fact]
    public void VhdxNtfsEntries_HaveWindowsMetadata_Sync()
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.vhdx");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        AssertNtfsMetadata(results);
    }

    private static void AssertNtfsMetadata(IList<FileEntry> results)
    {
        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            // NTFS provides Windows file attributes
            Assert.Equal(FileAttributes.Archive, entry.Metadata!.FileAttributes);
            // NTFS provides security descriptors
            Assert.NotNull(entry.Metadata.SecurityDescriptorSddl);
            Assert.Contains("O:", entry.Metadata.SecurityDescriptorSddl); // Owner present
            Assert.Contains("D:", entry.Metadata.SecurityDescriptorSddl); // DACL present
            // NTFS does not provide Unix metadata
            Assert.Null(entry.Metadata.Mode);
            Assert.Null(entry.Metadata.Uid);
            Assert.Null(entry.Metadata.Gid);
        }
    }

    [Fact]
    public async Task WimEntries_HaveWindowsFileAttributes()
    {
        // WIM extraction is only supported on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.wim");
        var results = await extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }).ToListAsync();

        AssertWimMetadata(results);
    }

    [Fact]
    public void WimEntries_HaveWindowsFileAttributes_Sync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "TestData.wim");
        var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false }).ToList();

        AssertWimMetadata(results);
    }

    private static void AssertWimMetadata(IList<FileEntry> results)
    {
        Assert.NotEmpty(results);
        foreach (var entry in results)
        {
            Assert.NotNull(entry.Metadata);
            Assert.Equal(FileAttributes.Archive, entry.Metadata!.FileAttributes);
            Assert.Null(entry.Metadata.Mode);
            Assert.Null(entry.Metadata.Uid);
            Assert.Null(entry.Metadata.Gid);
            // This WIM records no security descriptors, so an empty SDDL is normalized to null
            Assert.Null(entry.Metadata.SecurityDescriptorSddl);
        }
    }
}
