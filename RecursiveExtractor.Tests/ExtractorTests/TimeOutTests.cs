using Microsoft.CST.RecursiveExtractor;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class TimeOutTests : IClassFixture<BaseExtractorTestClass>
{
    [Theory]
    [InlineData("TestData.7z", false)]
    [InlineData("TestData.tar", false)]
    [InlineData("TestData.rar", false)]
    [InlineData("TestData.rar4", false)]
    [InlineData("TestData.tar.bz2", false)]
    [InlineData("TestData.tar.gz", false)]
    [InlineData("TestData.tar.xz", false)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", false)]
    [InlineData("TestData.a", false)]
    [InlineData("TestData.bsd.ar", false)]
    [InlineData("TestData.iso", false)]
    [InlineData("TestData.vhdx", false)]
    [InlineData("TestData.wim", false)]
    [InlineData("EmptyFile.txt", false)]
    [InlineData("TestData.zip", true)]
    [InlineData("TestData.7z", true)]
    [InlineData("TestData.tar", true)]
    [InlineData("TestData.rar", true)]
    [InlineData("TestData.rar4", true)]
    [InlineData("TestData.tar.bz2", true)]
    [InlineData("TestData.tar.gz", true)]
    [InlineData("TestData.tar.xz", true)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", true)]
    [InlineData("TestData.a", true)]
    [InlineData("TestData.bsd.ar", true)]
    [InlineData("TestData.iso", true)]
    [InlineData("TestData.vhdx", true)]
    [InlineData("TestData.wim", true)]
    [InlineData("EmptyFile.txt", true)]
    [InlineData("TestDataArchivesNested.Zip", true)]
    [InlineData("TestDataArchivesNested.Zip", false)]
    public void TimeoutTest(string fileName, bool parallel = false)
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
    [InlineData("TestData.7z", false)]
    [InlineData("TestData.tar", false)]
    [InlineData("TestData.rar", false)]
    [InlineData("TestData.rar4", false)]
    [InlineData("TestData.tar.bz2", false)]
    [InlineData("TestData.tar.gz", false)]
    [InlineData("TestData.tar.xz", false)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", false)]
    [InlineData("TestData.a", false)]
    [InlineData("TestData.bsd.ar", false)]
    [InlineData("TestData.iso", false)]
    [InlineData("TestData.vhdx", false)]
    [InlineData("TestData.wim", false)]
    [InlineData("EmptyFile.txt", false)]
    [InlineData("TestData.zip", true)]
    [InlineData("TestData.7z", true)]
    [InlineData("TestData.tar", true)]
    [InlineData("TestData.rar", true)]
    [InlineData("TestData.rar4", true)]
    [InlineData("TestData.tar.bz2", true)]
    [InlineData("TestData.tar.gz", true)]
    [InlineData("TestData.tar.xz", true)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", true)]
    [InlineData("TestData.a", true)]
    [InlineData("TestData.bsd.ar", true)]
    [InlineData("TestData.iso", true)]
    [InlineData("TestData.vhdx", true)]
    [InlineData("TestData.wim", true)]
    [InlineData("EmptyFile.txt", true)]
    [InlineData("TestDataArchivesNested.Zip", true)]
    [InlineData("TestDataArchivesNested.Zip", false)]
    public async Task TimeoutTestAsync(string fileName, bool parallel = false)
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
