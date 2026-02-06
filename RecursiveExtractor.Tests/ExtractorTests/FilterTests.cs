using Microsoft.CST.RecursiveExtractor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

[Collection(ExtractorTestCollection.Name)]
public class FilterTests
{
    [Theory]
    [InlineData("TestData.zip")]
    [InlineData("TestData.7z")]
    [InlineData("TestData.tar")]
    [InlineData("TestData.rar")]
    [InlineData("TestData.rar4")]
    [InlineData("TestData.tar.bz2")]
    [InlineData("TestData.tar.gz")]
    [InlineData("TestData.tar.xz")]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
    [InlineData("TestData.a", 0)]
    [InlineData("TestData.bsd.ar", 0)]
    [InlineData("TestData.iso")]
    [InlineData("TestData.vhdx")]
    [InlineData("TestData.wim")]
    [InlineData("EmptyFile.txt", 0)]
    [InlineData("TestDataArchivesNested.Zip", 9)]
    public async Task ExtractArchiveAsyncAllowFiltered(string fileName, int expectedNumFiles = 1)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.ExtractAsync(path,
            new ExtractorOptions() { AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        var numResults = 0;
        await foreach (var result in results)
        {
            numResults++;
        }

        Assert.Equal(expectedNumFiles, numResults);
    }

    [Theory]
    [InlineData("TestData.zip")]
    [InlineData("TestData.7z")]
    [InlineData("TestData.tar")]
    [InlineData("TestData.rar")]
    [InlineData("TestData.rar4")]
    [InlineData("TestData.tar.bz2")]
    [InlineData("TestData.tar.gz")]
    [InlineData("TestData.tar.xz")]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
    [InlineData("TestData.a", 0)]
    [InlineData("TestData.bsd.ar", 0)]
    [InlineData("TestData.iso")]
    [InlineData("TestData.vhdx")]
    [InlineData("TestData.wim")]
    [InlineData("EmptyFile.txt", 0)]
    [InlineData("TestDataArchivesNested.Zip", 9)]
    public void ExtractArchiveAllowFiltered(string fileName, int expectedNumFiles = 1)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [InlineData("TestData.zip")]
    [InlineData("TestData.7z")]
    [InlineData("TestData.tar")]
    [InlineData("TestData.rar")]
    [InlineData("TestData.rar4")]
    [InlineData("TestData.tar.bz2")]
    [InlineData("TestData.tar.gz")]
    [InlineData("TestData.tar.xz")]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
    [InlineData("TestData.a", 0)]
    [InlineData("TestData.bsd.ar", 0)]
    [InlineData("TestData.iso")]
    [InlineData("TestData.vhdx")]
    [InlineData("TestData.wim")]
    [InlineData("EmptyFile.txt", 0)]
    [InlineData("TestDataArchivesNested.Zip", 9)]
    public void ExtractArchiveParallelAllowFiltered(string fileName, int expectedNumFiles = 1)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { Parallel = true, AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [InlineData("TestData.zip", 4)]
    [InlineData("TestData.7z")]
    [InlineData("TestData.tar", 5)]
    [InlineData("TestData.rar")]
    [InlineData("TestData.rar4")]
    [InlineData("TestData.tar.bz2", 5)]
    [InlineData("TestData.tar.gz", 5)]
    [InlineData("TestData.tar.xz")]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
    [InlineData("TestData.a", 3)]
    [InlineData("TestData.bsd.ar", 3)]
    [InlineData("TestData.iso")]
    [InlineData("TestData.vhdx")]
    [InlineData("TestData.wim")]
    [InlineData("EmptyFile.txt", 1)]
    [InlineData("TestDataArchivesNested.Zip", 45)]
    public void ExtractArchiveDenyFiltered(string fileName, int expectedNumFiles = 2)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path, new ExtractorOptions() { DenyFilters = new string[] { "**/Bar/**" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [InlineData("TestData.zip", 4)]
    [InlineData("TestData.7z")]
    [InlineData("TestData.tar", 5)]
    [InlineData("TestData.rar")]
    [InlineData("TestData.rar4")]
    [InlineData("TestData.tar.bz2", 5)]
    [InlineData("TestData.tar.gz", 5)]
    [InlineData("TestData.tar.xz")]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
    [InlineData("TestData.a", 3)]
    [InlineData("TestData.bsd.ar", 3)]
    [InlineData("TestData.iso")]
    [InlineData("TestData.vhdx")]
    [InlineData("TestData.wim")]
    [InlineData("EmptyFile.txt", 1)]
    [InlineData("TestDataArchivesNested.Zip", 45)]
    public void ExtractArchiveParallelDenyFiltered(string fileName, int expectedNumFiles = 2)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { Parallel = true, DenyFilters = new string[] { "**/Bar/**" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [InlineData("TestData.zip", 4)]
    [InlineData("TestData.7z")]
    [InlineData("TestData.tar", 5)]
    [InlineData("TestData.rar")]
    [InlineData("TestData.rar4")]
    [InlineData("TestData.tar.bz2", 5)]
    [InlineData("TestData.tar.gz", 5)]
    [InlineData("TestData.tar.xz")]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
    [InlineData("TestData.a", 3)]
    [InlineData("TestData.bsd.ar", 3)]
    [InlineData("TestData.iso")]
    [InlineData("TestData.vhdx")]
    [InlineData("TestData.wim")]
    [InlineData("EmptyFile.txt", 1)]
    [InlineData("TestDataArchivesNested.Zip", 45)]
    public async Task ExtractArchiveAsyncDenyFiltered(string fileName, int expectedNumFiles = 2)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results =
            extractor.ExtractAsync(path, new ExtractorOptions() { DenyFilters = new string[] { "**/Bar/**" } });
        var numResults = 0;
        await foreach (var result in results)
        {
            numResults++;
        }

        Assert.Equal(expectedNumFiles, numResults);
    }

    [Theory]
    [InlineData(ArchiveFileType.ZIP, new[] { ArchiveFileType.ZIP }, new ArchiveFileType[] { }, false)]
    [InlineData(ArchiveFileType.ZIP, new[] { ArchiveFileType.TAR }, new ArchiveFileType[] { }, true)]
    [InlineData(ArchiveFileType.ZIP, new ArchiveFileType[] { }, new[] { ArchiveFileType.ZIP }, true)]
    [InlineData(ArchiveFileType.TAR, new ArchiveFileType[] { }, new[] { ArchiveFileType.ZIP }, false)]
    [InlineData(ArchiveFileType.ZIP, new[] { ArchiveFileType.ZIP }, new[] { ArchiveFileType.ZIP }, false)]
    public void TestArchiveTypeFilters(ArchiveFileType typeToCheck, IEnumerable<ArchiveFileType> denyTypes,
        IEnumerable<ArchiveFileType> allowTypes, bool expected)
    {
        ExtractorOptions opts = new() { AllowTypes = allowTypes, DenyTypes = denyTypes };
        Assert.Equal(expected, opts.IsAcceptableType(typeToCheck));
    }
}
