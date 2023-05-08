using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class TestQuinesAndSlip : BaseExtractorTestClass
{
    [DataTestMethod]
    [DataRow("zip-slip-win.zip")]
    [DataRow("zip-slip-win.tar")]
    [DataRow("zip-slip.zip")]
    [DataRow("zip-slip.tar")]
    public void TestZipSlip(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        var results = extractor.Extract(path, new ExtractorOptions()).ToList();
        Assert.IsTrue(results.All(x => !x.FullPath.Contains("..")));
    }

    [DataTestMethod]
    [DataRow("10GB.7z.bz2")]
    [DataRow("10GB.gz.bz2")]
    [DataRow("10GB.rar.bz2")]
    [DataRow("10GB.xz.bz2")]
    [DataRow("10GB.zip.bz2")]
    [DataRow("zblg.zip")]
    [DataRow("zbsm.zip")]
    [ExpectedException(typeof(OverflowException))]
    public void TestQuineBombs(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        _ = extractor.Extract(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToList();
    }
    [DataTestMethod]
    [DataRow("zoneinfo-2010g.tar")] // This isn't itself a bomb, but sharpcompress has an issue which results in an overflow exception
    [ExpectedException(typeof(OverflowException))]
    public void TestMalformedArchives(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Malformed", fileName);
        _ = extractor.Extract(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToList();
    }
}