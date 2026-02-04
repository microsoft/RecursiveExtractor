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
        var results = await extractor.ExtractAsync(path, new ExtractorOptions()).ToListAsync(TestContext.CancellationTokenSource.Token);
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
    public void TestQuineBombs(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        Assert.ThrowsExactly<OverflowException>(() =>
        {
            _ = extractor.Extract(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToList();
        });
    }
    
    [TestMethod]        
    [DynamicData(nameof(QuineBombNames))]
    public async Task TestQuineBombsAsync(string fileName)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
        await Assert.ThrowsExactlyAsync<OverflowException>(async () =>
        {
            _ = await extractor.ExtractAsync(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToListAsync(TestContext.CancellationTokenSource.Token);
        });
    }
}