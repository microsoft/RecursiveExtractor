using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class MiscTests
{
    [DataTestMethod]
    [DataRow("TestDataCorrupt.tar", false, 0, 1)]
    [DataRow("TestDataCorrupt.tar", true, 1, 1)]
    [DataRow("TestDataCorrupt.tar.zip", false, 0, 2)]
    [DataRow("TestDataCorrupt.tar.zip", true, 0, 2)]
    public async Task ExtractCorruptArchiveAsync(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures, int expectedNumFiles)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = await extractor.ExtractAsync(path,
            new ExtractorOptions() { RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToListAsync();
            

        Assert.AreEqual(expectedNumFiles, results.Count);
        var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
        Assert.AreEqual(expectedNumFailures, actualNumberOfFailedArchives);
    }

    [DataTestMethod]
    [DataRow("Lorem.txt", true, 1)]
    [DataRow("Lorem.txt", false, 0)]
    public async Task ExtractFlatFileAsync(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestData", fileName);
        var results = await extractor.ExtractAsync(path, new ExtractorOptions(){ RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToListAsync();
            
        Assert.AreEqual(1, results.Count);
        var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
        Assert.AreEqual(expectedNumFailures, actualNumberOfFailedArchives);
    }
    
    

        [DataTestMethod]
        [DataRow("TestDataCorrupt.tar", false, 0, 1)]
        [DataRow("TestDataCorrupt.tar", true, 1, 1)]
        [DataRow("TestDataCorrupt.tar.zip", false, 0, 2)]
        [DataRow("TestDataCorrupt.tar.zip", true, 0, 2)]
        [DataRow("TestDataCorruptWim.zip", true, 0, 0)]
        public void ExtractCorruptArchive(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions(){ RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToList();

            Assert.AreEqual(expectedNumFiles, results.Count);
            var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
            Assert.AreEqual(expectedNumFailures, actualNumberOfFailedArchives);
        }

        [DataTestMethod]
        [DataRow("Lorem.txt", true, 1)]
        [DataRow("Lorem.txt", false, 0)]
        public void ExtractFlatFile(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestData", fileName);
            var results = extractor.Extract(path, new ExtractorOptions(){ RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToList();
            
            Assert.AreEqual(1, results.Count);
            var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
            Assert.AreEqual(expectedNumFailures, actualNumberOfFailedArchives);
        }

        [DataTestMethod]
        [DataRow("EmptyFile.txt")]
        [DataRow("TestData.zip", ".zip")]
        public void ExtractAsRaw(string fileName, string? RawExtension = null)
        {
            var extractor = new Extractor();
            var options = new ExtractorOptions()
            {
                RawExtensions = RawExtension is null ? new List<string>() : new List<string>() { RawExtension }
            };
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);

            var results = extractor.Extract(path, options);
            Assert.AreEqual(1, results.Count());
        }
}