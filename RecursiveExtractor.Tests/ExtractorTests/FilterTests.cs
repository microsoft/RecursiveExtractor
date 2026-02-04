using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class FilterTests : BaseExtractorTestClass
{
    [TestMethod]
    [DataRow("TestData.zip")]
    [DataRow("TestData.7z")]
    [DataRow("TestData.tar")]
    [DataRow("TestData.rar")]
    [DataRow("TestData.rar4")]
    [DataRow("TestData.tar.bz2")]
    [DataRow("TestData.tar.gz")]
    [DataRow("TestData.tar.xz")]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
    [DataRow("TestData.a", 0)]
    [DataRow("TestData.bsd.ar", 0)]
    [DataRow("TestData.iso")]
    [DataRow("TestData.vhdx")]
    [DataRow("TestData.wim")]
    [DataRow("EmptyFile.txt", 0)]
    [DataRow("TestDataArchivesNested.Zip", 9)]
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

        Assert.AreEqual(expectedNumFiles, numResults);
    }

    [TestMethod]
    [DataRow("TestData.zip")]
    [DataRow("TestData.7z")]
    [DataRow("TestData.tar")]
    [DataRow("TestData.rar")]
    [DataRow("TestData.rar4")]
    [DataRow("TestData.tar.bz2")]
    [DataRow("TestData.tar.gz")]
    [DataRow("TestData.tar.xz")]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
    [DataRow("TestData.a", 0)]
    [DataRow("TestData.bsd.ar", 0)]
    [DataRow("TestData.iso")]
    [DataRow("TestData.vhdx")]
    [DataRow("TestData.wim")]
    [DataRow("EmptyFile.txt", 0)]
    [DataRow("TestDataArchivesNested.Zip", 9)]
    public void ExtractArchiveAllowFiltered(string fileName, int expectedNumFiles = 1)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        Assert.AreEqual(expectedNumFiles, results.Count());
    }

    [TestMethod]
    [DataRow("TestData.zip")]
    [DataRow("TestData.7z")]
    [DataRow("TestData.tar")]
    [DataRow("TestData.rar")]
    [DataRow("TestData.rar4")]
    [DataRow("TestData.tar.bz2")]
    [DataRow("TestData.tar.gz")]
    [DataRow("TestData.tar.xz")]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
    [DataRow("TestData.a", 0)]
    [DataRow("TestData.bsd.ar", 0)]
    [DataRow("TestData.iso")]
    [DataRow("TestData.vhdx")]
    [DataRow("TestData.wim")]
    [DataRow("EmptyFile.txt", 0)]
    [DataRow("TestDataArchivesNested.Zip", 9)]
    public void ExtractArchiveParallelAllowFiltered(string fileName, int expectedNumFiles = 1)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { Parallel = true, AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
        Assert.AreEqual(expectedNumFiles, results.Count());
    }

    [TestMethod]
    [DataRow("TestData.zip", 4)]
    [DataRow("TestData.7z")]
    [DataRow("TestData.tar", 5)]
    [DataRow("TestData.rar")]
    [DataRow("TestData.rar4")]
    [DataRow("TestData.tar.bz2", 5)]
    [DataRow("TestData.tar.gz", 5)]
    [DataRow("TestData.tar.xz")]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
    [DataRow("TestData.a", 3)]
    [DataRow("TestData.bsd.ar", 3)]
    [DataRow("TestData.iso")]
    [DataRow("TestData.vhdx")]
    [DataRow("TestData.wim")]
    [DataRow("EmptyFile.txt", 1)]
    [DataRow("TestDataArchivesNested.Zip", 45)]
    public void ExtractArchiveDenyFiltered(string fileName, int expectedNumFiles = 2)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path, new ExtractorOptions() { DenyFilters = new string[] { "**/Bar/**" } });
        Assert.AreEqual(expectedNumFiles, results.Count());
    }

    [TestMethod]
    [DataRow("TestData.zip", 4)]
    [DataRow("TestData.7z")]
    [DataRow("TestData.tar", 5)]
    [DataRow("TestData.rar")]
    [DataRow("TestData.rar4")]
    [DataRow("TestData.tar.bz2", 5)]
    [DataRow("TestData.tar.gz", 5)]
    [DataRow("TestData.tar.xz")]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
    [DataRow("TestData.a", 3)]
    [DataRow("TestData.bsd.ar", 3)]
    [DataRow("TestData.iso")]
    [DataRow("TestData.vhdx")]
    [DataRow("TestData.wim")]
    [DataRow("EmptyFile.txt", 1)]
    [DataRow("TestDataArchivesNested.Zip", 45)]
    public void ExtractArchiveParallelDenyFiltered(string fileName, int expectedNumFiles = 2)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path,
            new ExtractorOptions() { Parallel = true, DenyFilters = new string[] { "**/Bar/**" } });
        Assert.AreEqual(expectedNumFiles, results.Count());
    }

    [TestMethod]
    [DataRow("TestData.zip", 4)]
    [DataRow("TestData.7z")]
    [DataRow("TestData.tar", 5)]
    [DataRow("TestData.rar")]
    [DataRow("TestData.rar4")]
    [DataRow("TestData.tar.bz2", 5)]
    [DataRow("TestData.tar.gz", 5)]
    [DataRow("TestData.tar.xz")]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
    [DataRow("TestData.a", 3)]
    [DataRow("TestData.bsd.ar", 3)]
    [DataRow("TestData.iso")]
    [DataRow("TestData.vhdx")]
    [DataRow("TestData.wim")]
    [DataRow("EmptyFile.txt", 1)]
    [DataRow("TestDataArchivesNested.Zip", 45)]
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

        Assert.AreEqual(expectedNumFiles, numResults);
    }

    [TestMethod]
    [DataRow(ArchiveFileType.ZIP, new[] { ArchiveFileType.ZIP }, new ArchiveFileType[] { }, false)]
    [DataRow(ArchiveFileType.ZIP, new[] { ArchiveFileType.TAR }, new ArchiveFileType[] { }, true)]
    [DataRow(ArchiveFileType.ZIP, new ArchiveFileType[] { }, new[] { ArchiveFileType.ZIP }, true)]
    [DataRow(ArchiveFileType.TAR, new ArchiveFileType[] { }, new[] { ArchiveFileType.ZIP }, false)]
    [DataRow(ArchiveFileType.ZIP, new[] { ArchiveFileType.ZIP }, new[] { ArchiveFileType.ZIP }, false)]
    public void TestArchiveTypeFilters(ArchiveFileType typeToCheck, IEnumerable<ArchiveFileType> denyTypes,
        IEnumerable<ArchiveFileType> allowTypes, bool expected)
    {
        ExtractorOptions opts = new() { AllowTypes = allowTypes, DenyTypes = denyTypes };
        Assert.AreEqual(expected, opts.IsAcceptableType(typeToCheck));
    }
}