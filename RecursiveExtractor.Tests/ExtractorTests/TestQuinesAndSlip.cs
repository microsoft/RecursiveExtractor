using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archives.Tar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class TestQuinesAndSlip : BaseExtractorTestClass
{
    public static IEnumerable<object[]> ZipSlipNames
    {
        get
        {
            return new[]
            { 
                new object [] { "zip-slip-win.zip" },
                new object [] { "zip-slip-win.tar" },
                new object [] { "zip-slip.zip" },
                new object [] { "zip-slip.tar" }
            };
        }
    }
    
    [TestMethod]
    [DynamicData(nameof(ZipSlipNames))]
    public void TestZipSlip(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        var results = extractor.Extract(path, new ExtractorOptions()).ToList();
        Assert.IsTrue(results.All(x => !x.FullPath.Contains("..")));
    }
    
    [TestMethod]
    [DynamicData(nameof(ZipSlipNames))]
    public async Task TestZipSlipAsync(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        var results = await extractor.ExtractAsync(path, new ExtractorOptions()).ToListAsync();
        Assert.IsTrue(results.All(x => !x.FullPath.Contains("..")));
    }
    
    public static IEnumerable<object[]> QuineBombNames
    {
        get
        {
            return new[]
            { 
                new object [] { "10GB.7z.bz2" },
                new object [] { "10GB.gz.bz2" },
                new object [] { "10GB.rar.bz2" },
                new object [] { "10GB.xz.bz2" },
                new object [] { "10GB.zip.bz2" },
                new object [] { "zblg.zip" },
                new object [] { "zbsm.zip" }
            };
        }
    }
    
    [TestMethod]        
    [DynamicData(nameof(QuineBombNames))]
    [ExpectedException(typeof(OverflowException))]
    public void TestQuineBombs(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        _ = extractor.Extract(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToList();
    }
    
    [TestMethod]        
    [DynamicData(nameof(QuineBombNames))]
    [ExpectedException(typeof(OverflowException))]
    public async Task TestQuineBombsAsync(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        _ = await extractor.ExtractAsync(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToListAsync();
    }
    
    [DataTestMethod]
    // This is a malformed archive that used to trigger overflow with sharpcompress https://github.com/adamhathcock/sharpcompress/issues/736
    // We now perform best effort extraction and test validates that exception isn't thrown
    [DataRow("zoneinfo-2010g.tar", 63)]
    public void TestMalformedArchives(string fileName, int expectedNum)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Malformed", fileName);
        var extractor = new Extractor();
        var entries = extractor.Extract(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToList();
        Assert.AreEqual(expectedNum, entries.Count);
    }
    
    [DataTestMethod]
    // This is a malformed archive that used to trigger overflow with sharpcompress https://github.com/adamhathcock/sharpcompress/issues/736
    // We now perform best effort extraction and test validates that exception isn't thrown
    [DataRow("zoneinfo-2010g.tar", 63)] 
    public async Task TestMalformedArchivesAsync(string fileName, int expectedNum)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Malformed", fileName);
        var extractor = new Extractor();
        var entries = await extractor.ExtractAsync(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToListAsync();
        Assert.AreEqual(expectedNum, entries.Count);
    }
}