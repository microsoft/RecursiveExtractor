using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

[Collection(ExtractorTestCollection.Name)]
public class DisposeBehaviorTests
{
    /// <summary>
    /// Test data for dispose-while-enumerating tests. WIM is Windows-only so conditionally included.
    /// TestDataArchivesNested.zip count varies by platform because embedded WIM is only extracted on Windows.
    /// </summary>
    public static TheoryData<string, int, bool> DisposeData
    {
        get
        {
            var data = new TheoryData<string, int, bool>
            {
                { "TestData.7z", 3, false },
                { "TestData.tar", 6, false },
                { "TestData.rar", 3, false },
                { "TestData.rar4", 3, false },
                { "TestData.tar.bz2", 6, false },
                { "TestData.tar.gz", 6, false },
                { "TestData.tar.xz", 3, false },
                { "sysvbanner_1.0-17fakesync1_amd64.deb", 8, false },
                { "TestData.a", 3, false },
                { "TestData.bsd.ar", 3, false },
                { "TestData.iso", 3, false },
                { "TestData.vhdx", 3, false },
                { "EmptyFile.txt", 1, false },
                { "TestData.zip", 5, true },
                { "TestData.7z", 3, true },
                { "TestData.tar", 6, true },
                { "TestData.rar", 3, true },
                { "TestData.rar4", 3, true },
                { "TestData.tar.bz2", 6, true },
                { "TestData.tar.gz", 6, true },
                { "TestData.tar.xz", 3, true },
                { "sysvbanner_1.0-17fakesync1_amd64.deb", 8, true },
                { "TestData.a", 3, true },
                { "TestData.bsd.ar", 3, true },
                { "TestData.iso", 3, true },
                { "TestData.vhdx", 3, true },
                { "EmptyFile.txt", 1, true },
                { "TestDataArchivesNested.zip", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 54 : 52, true },
                { "TestDataArchivesNested.zip", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 54 : 52, false },
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                data.Add("TestData.wim", 3, false);
                data.Add("TestData.wim", 3, true);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DisposeData))]
    public void ExtractArchiveAndDisposeWhileEnumerating(string fileName, int expectedNumFiles,
        bool parallel)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path, new ExtractorOptions() { Parallel = parallel });
        var disposedResults = new List<FileEntry>();
        foreach (var file in results)
        {
            disposedResults.Add(file);
            using var theStream = file.Content;
            // Do something with the stream.
            _ = theStream.ReadByte();
        }

        Assert.Equal(expectedNumFiles, disposedResults.Count);
        foreach (var disposedResult in disposedResults)
        {
            Assert.Throws<ObjectDisposedException>(() => disposedResult.Content.Position);
        }
    }

    [Theory]
    [MemberData(nameof(DisposeData))]
    public async Task ExtractArchiveAndDisposeWhileEnumeratingAsync(string fileName, int expectedNumFiles,
        bool parallel)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.ExtractAsync(path, new ExtractorOptions() { Parallel = parallel });
        var disposedResults = new List<FileEntry>();
        await foreach (var file in results)
        {
            disposedResults.Add(file);
            using var theStream = file.Content;
            // Do something with the stream.
            _ = theStream.ReadByte();
        }

        Assert.Equal(expectedNumFiles, disposedResults.Count);
        foreach (var disposedResult in disposedResults)
        {
            Assert.Throws<ObjectDisposedException>(() => disposedResult.Content.Position);
        }
    }

    [Theory]
    [InlineData("TestData.zip")]
    public void EnsureDisposedWithExtractToDirectory(string fileName)
    {
        var directory = TestPathHelpers.GetFreshTestDirectory();
        var copyDirectory = TestPathHelpers.GetFreshTestDirectory();
        Directory.CreateDirectory(copyDirectory);
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        // Make a copy of the archive so it can be deleted later to confirm properly disposed
        var copyPath = Path.Combine(copyDirectory, fileName);
        File.Copy(path, copyPath);
        var extractor = new Extractor();
        extractor.ExtractToDirectory(directory, copyPath);
        File.Delete(copyPath);
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (DirectoryNotFoundException) { }
        try
        {
            if (Directory.Exists(copyDirectory))
            {
                Directory.Delete(copyDirectory, true);
            }
        }
        catch (DirectoryNotFoundException) { }
    }

    [Theory]
    [InlineData("TestData.zip")]
    public async Task EnsureDisposedWithExtractToDirectoryAsync(string fileName)
    {
        var directory = TestPathHelpers.GetFreshTestDirectory();
        var copyDirectory = TestPathHelpers.GetFreshTestDirectory();
        Directory.CreateDirectory(copyDirectory);
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        // Make a copy of the archive so it can be deleted later to confirm properly disposed
        var copyPath = Path.Combine(copyDirectory, fileName);
        File.Copy(path, copyPath);
        var extractor = new Extractor();
        await extractor.ExtractToDirectoryAsync(directory, copyPath);
        File.Delete(copyPath);
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (DirectoryNotFoundException) { }
        try
        {
            if (Directory.Exists(copyDirectory))
            {
                Directory.Delete(copyDirectory, true);
            }
        }
        catch (DirectoryNotFoundException) { }
    }
}