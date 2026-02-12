using Microsoft.CST.RecursiveExtractor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class FilterTests
{
    /// <summary>
    /// Test data for allow filter tests. WIM is Windows-only so conditionally included.
    /// TestDataArchivesNested.zip count varies by platform because embedded WIM is only extracted on Windows.
    /// </summary>
    public static TheoryData<string, int> AllowFilterData
    {
        get
        {
            var data = new TheoryData<string, int>
            {
                { "TestData.zip", 1 },
                { "TestData.7z", 1 },
                { "TestData.tar", 1 },
                { "TestData.rar", 1 },
                { "TestData.rar4", 1 },
                { "TestData.tar.bz2", 1 },
                { "TestData.tar.gz", 1 },
                { "TestData.tar.xz", 1 },
                { "sysvbanner_1.0-17fakesync1_amd64.deb", 0 },
                { "TestData.a", 0 },
                { "TestData.bsd.ar", 0 },
                { "TestData.iso", 1 },
                { "TestData.vhdx", 1 },
                { "EmptyFile.txt", 0 },
                { "TestDataArchivesNested.zip", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 9 : 8 },
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                data.Add("TestData.wim", 1);
            }
            return data;
        }
    }

    /// <summary>
    /// Test data for deny filter tests. WIM is Windows-only so conditionally included.
    /// TestDataArchivesNested.zip count varies by platform because embedded WIM is only extracted on Windows.
    /// </summary>
    public static TheoryData<string, int> DenyFilterData
    {
        get
        {
            var data = new TheoryData<string, int>
            {
                { "TestData.zip", 4 },
                { "TestData.7z", 2 },
                { "TestData.tar", 5 },
                { "TestData.rar", 2 },
                { "TestData.rar4", 2 },
                { "TestData.tar.bz2", 5 },
                { "TestData.tar.gz", 5 },
                { "TestData.tar.xz", 2 },
                { "sysvbanner_1.0-17fakesync1_amd64.deb", 8 },
                { "TestData.a", 3 },
                { "TestData.bsd.ar", 3 },
                { "TestData.iso", 2 },
                { "TestData.vhdx", 2 },
                { "EmptyFile.txt", 1 },
                { "TestDataArchivesNested.zip", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 45 : 44 },
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                data.Add("TestData.wim", 2);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllowFilterData))]
    public async Task ExtractArchiveAsyncAllowFiltered(string fileName, int expectedNumFiles)
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
    [MemberData(nameof(AllowFilterData))]
    public void ExtractArchiveAllowFiltered(string fileName, int expectedNumFiles)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [MemberData(nameof(AllowFilterData))]
    public void ExtractArchiveParallelAllowFiltered(string fileName, int expectedNumFiles)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { Parallel = true, AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [MemberData(nameof(DenyFilterData))]
    public void ExtractArchiveDenyFiltered(string fileName, int expectedNumFiles)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path, new ExtractorOptions() { DenyFilters = new string[] { "**/Bar/**" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [MemberData(nameof(DenyFilterData))]
    public void ExtractArchiveParallelDenyFiltered(string fileName, int expectedNumFiles)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { Parallel = true, DenyFilters = new string[] { "**/Bar/**" } });
        Assert.Equal(expectedNumFiles, results.Count());
    }

    [Theory]
    [MemberData(nameof(DenyFilterData))]
    public async Task ExtractArchiveAsyncDenyFiltered(string fileName, int expectedNumFiles)
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
