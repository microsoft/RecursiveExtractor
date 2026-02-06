using Microsoft.CST.RecursiveExtractor;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class TimeOutTests : BaseExtractorTestClass
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
    public void TimeoutTest(string fileName, int _expectedNumFiles = 3, bool parallel = false)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        Assert.Throws<TimeoutException>(() =>
        {
            var results = extractor.Extract(path,
                new ExtractorOptions()
                {
                    Parallel = parallel, EnableTiming = true, Timeout = new TimeSpan(0, 0, 0, 0, 0)
                });
            int count = 0;
            foreach (var result in results)
            {
                count++;
            }

            // We should not be able to get to all the files
            Assert.Fail("Should have thrown TimeoutException");
        });
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
    public async Task TimeoutTestAsync(string fileName, int _expectedNumFiles = 3, bool parallel = false)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            var results = extractor.ExtractAsync(path,
                new ExtractorOptions()
                {
                    Parallel = parallel, EnableTiming = true, Timeout = new TimeSpan(0, 0, 0, 0, 0)
                });
            int count = 0;
            await foreach (var result in results)
            {
                count++;
            }

            // We should not be able to get to all the files
            Assert.Fail("Should have thrown TimeoutException");
        });
    }
}