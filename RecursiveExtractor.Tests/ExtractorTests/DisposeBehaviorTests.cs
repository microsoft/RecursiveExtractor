using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class DisposeBehaviorTests : BaseExtractorTestClass
{
    [Theory]
    [InlineData("TestData.7z", 3, false)]
    [InlineData("TestData.tar", 6, false)]
    [InlineData("TestData.rar", 3, false)]
    [InlineData("TestData.rar4", 3, false)]
    [InlineData("TestData.tar.bz2", 6, false)]
    [InlineData("TestData.tar.gz", 6, false)]
    [InlineData("TestData.tar.xz", 3, false)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8, false)]
    [InlineData("TestData.a", 3, false)]
    [InlineData("TestData.bsd.ar", 3, false)]
    [InlineData("TestData.iso", 3, false)]
    [InlineData("TestData.vhdx", 3, false)]
    [InlineData("TestData.wim", 3, false)]
    [InlineData("EmptyFile.txt", 1, false)]
    [InlineData("TestData.zip", 5, true)]
    [InlineData("TestData.7z", 3, true)]
    [InlineData("TestData.tar", 6, true)]
    [InlineData("TestData.rar", 3, true)]
    [InlineData("TestData.rar4", 3, true)]
    [InlineData("TestData.tar.bz2", 6, true)]
    [InlineData("TestData.tar.gz", 6, true)]
    [InlineData("TestData.tar.xz", 3, true)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8, true)]
    [InlineData("TestData.a", 3, true)]
    [InlineData("TestData.bsd.ar", 3, true)]
    [InlineData("TestData.iso", 3, true)]
    [InlineData("TestData.vhdx", 3, true)]
    [InlineData("TestData.wim", 3, true)]
    [InlineData("EmptyFile.txt", 1, true)]
    [InlineData("TestDataArchivesNested.Zip", 54, true)]
    [InlineData("TestDataArchivesNested.Zip", 54, false)]
    public void ExtractArchiveAndDisposeWhileEnumerating(string fileName, int expectedNumFiles = 3,
        bool parallel = false)
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
    [InlineData("TestData.7z", 3, false)]
    [InlineData("TestData.tar", 6, false)]
    [InlineData("TestData.rar", 3, false)]
    [InlineData("TestData.rar4", 3, false)]
    [InlineData("TestData.tar.bz2", 6, false)]
    [InlineData("TestData.tar.gz", 6, false)]
    [InlineData("TestData.tar.xz", 3, false)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8, false)]
    [InlineData("TestData.a", 3, false)]
    [InlineData("TestData.bsd.ar", 3, false)]
    [InlineData("TestData.iso", 3, false)]
    [InlineData("TestData.vhdx", 3, false)]
    [InlineData("TestData.wim", 3, false)]
    [InlineData("EmptyFile.txt", 1, false)]
    [InlineData("TestData.zip", 5, true)]
    [InlineData("TestData.7z", 3, true)]
    [InlineData("TestData.tar", 6, true)]
    [InlineData("TestData.rar", 3, true)]
    [InlineData("TestData.rar4", 3, true)]
    [InlineData("TestData.tar.bz2", 6, true)]
    [InlineData("TestData.tar.gz", 6, true)]
    [InlineData("TestData.tar.xz", 3, true)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8, true)]
    [InlineData("TestData.a", 3, true)]
    [InlineData("TestData.bsd.ar", 3, true)]
    [InlineData("TestData.iso", 3, true)]
    [InlineData("TestData.vhdx", 3, true)]
    [InlineData("TestData.wim", 3, true)]
    [InlineData("EmptyFile.txt", 1, true)]
    [InlineData("TestDataArchivesNested.Zip", 54, true)]
    [InlineData("TestDataArchivesNested.Zip", 54, false)]
    public async Task ExtractArchiveAndDisposeWhileEnumeratingAsync(string fileName, int expectedNumFiles = 3,
        bool parallel = false)
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
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
        if (Directory.Exists(copyDirectory)) {
            Directory.Delete(copyDirectory, true);
        }
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
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
        if (Directory.Exists(copyDirectory))
        {
            Directory.Delete(copyDirectory, true);
        }
    }
}