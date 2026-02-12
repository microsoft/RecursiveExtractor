using Microsoft.CST.RecursiveExtractor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class MiscTests
{
    [Theory]
    [InlineData("TestDataCorrupt.tar", false, 0, 1)]
    [InlineData("TestDataCorrupt.tar", true, 1, 1)]
    [InlineData("TestDataCorrupt.tar.zip", false, 0, 2)]
    [InlineData("TestDataCorrupt.tar.zip", true, 0, 2)]
    public async Task ExtractCorruptArchiveAsync(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures, int expectedNumFiles)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = await extractor.ExtractAsync(path,
            new ExtractorOptions() { RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToListAsync();
            

        Assert.Equal(expectedNumFiles, results.Count);
        var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
        Assert.Equal(expectedNumFailures, actualNumberOfFailedArchives);
    }

    [Theory]
    [InlineData("Lorem.txt", true, 1)]
    [InlineData("Lorem.txt", false, 0)]
    public async Task ExtractFlatFileAsync(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestData", fileName);
        var results = await extractor.ExtractAsync(path, new ExtractorOptions(){ RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToListAsync();
            
        Assert.Single(results);
        var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
        Assert.Equal(expectedNumFailures, actualNumberOfFailedArchives);
    }
    
    

        [Theory]
        [InlineData("TestDataCorrupt.tar", false, 0, 1)]
        [InlineData("TestDataCorrupt.tar", true, 1, 1)]
        [InlineData("TestDataCorrupt.tar.zip", false, 0, 2)]
        [InlineData("TestDataCorrupt.tar.zip", true, 0, 2)]
        [InlineData("TestDataCorruptWim.zip", true, 0, 0)]
        public void ExtractCorruptArchive(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures, int expectedNumFiles)
        {
            if (fileName.Contains("Wim", System.StringComparison.OrdinalIgnoreCase) &&
                !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions(){ RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToList();

            Assert.Equal(expectedNumFiles, results.Count);
            var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
            Assert.Equal(expectedNumFailures, actualNumberOfFailedArchives);
        }

        [Theory]
        [InlineData("Lorem.txt", true, 1)]
        [InlineData("Lorem.txt", false, 0)]
        public void ExtractFlatFile(string fileName, bool requireTopLevelToBeArchive, int expectedNumFailures)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestData", fileName);
            var results = extractor.Extract(path, new ExtractorOptions(){ RequireTopLevelToBeArchive = requireTopLevelToBeArchive }).ToList();
            
            Assert.Single(results);
            var actualNumberOfFailedArchives = results.Count(x => x.EntryStatus == FileEntryStatus.FailedArchive);
            Assert.Equal(expectedNumFailures, actualNumberOfFailedArchives);
        }

        [Theory]
        [InlineData("EmptyFile.txt")]
        [InlineData("TestData.zip", ".zip")]
        public void ExtractAsRaw(string fileName, string? RawExtension = null)
        {
            var extractor = new Extractor();
            var options = new ExtractorOptions()
            {
                RawExtensions = RawExtension is null ? new List<string>() : new List<string>() { RawExtension }
            };
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);

            var results = extractor.Extract(path, options);
            Assert.Single(results);
        }
}