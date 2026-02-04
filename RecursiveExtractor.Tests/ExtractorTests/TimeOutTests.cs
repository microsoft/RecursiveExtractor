using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class TimeOutTests : BaseExtractorTestClass
{
    [TestMethod]
    [DataRow("TestData.7z", 3, false)]
    [DataRow("TestData.tar", 6, false)]
    [DataRow("TestData.rar", 3, false)]
    [DataRow("TestData.rar4", 3, false)]
    [DataRow("TestData.tar.bz2", 6, false)]
    [DataRow("TestData.tar.gz", 6, false)]
    [DataRow("TestData.tar.xz", 3, false)]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8, false)]
    [DataRow("TestData.a", 3, false)]
    [DataRow("TestData.bsd.ar", 3, false)]
    [DataRow("TestData.iso", 3, false)]
    [DataRow("TestData.vhdx", 3, false)]
    [DataRow("TestData.wim", 3, false)]
    [DataRow("EmptyFile.txt", 1, false)]
    [DataRow("TestData.zip", 5, true)]
    [DataRow("TestData.7z", 3, true)]
    [DataRow("TestData.tar", 6, true)]
    [DataRow("TestData.rar", 3, true)]
    [DataRow("TestData.rar4", 3, true)]
    [DataRow("TestData.tar.bz2", 6, true)]
    [DataRow("TestData.tar.gz", 6, true)]
    [DataRow("TestData.tar.xz", 3, true)]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8, true)]
    [DataRow("TestData.a", 3, true)]
    [DataRow("TestData.bsd.ar", 3, true)]
    [DataRow("TestData.iso", 3, true)]
    [DataRow("TestData.vhdx", 3, true)]
    [DataRow("TestData.wim", 3, true)]
    [DataRow("EmptyFile.txt", 1, true)]
    [DataRow("TestDataArchivesNested.Zip", 54, true)]
    [DataRow("TestDataArchivesNested.Zip", 54, false)]
    public void TimeoutTest(string fileName, int expectedNumFiles = 3, bool parallel = false)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        Assert.ThrowsExactly<TimeoutException>(() =>
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
            Assert.Fail();
        });
    }

    [TestMethod]
    [DataRow("TestData.7z", 3, false)]
    [DataRow("TestData.tar", 6, false)]
    [DataRow("TestData.rar", 3, false)]
    [DataRow("TestData.rar4", 3, false)]
    [DataRow("TestData.tar.bz2", 6, false)]
    [DataRow("TestData.tar.gz", 6, false)]
    [DataRow("TestData.tar.xz", 3, false)]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8, false)]
    [DataRow("TestData.a", 3, false)]
    [DataRow("TestData.bsd.ar", 3, false)]
    [DataRow("TestData.iso", 3, false)]
    [DataRow("TestData.vhdx", 3, false)]
    [DataRow("TestData.wim", 3, false)]
    [DataRow("EmptyFile.txt", 1, false)]
    [DataRow("TestData.zip", 5, true)]
    [DataRow("TestData.7z", 3, true)]
    [DataRow("TestData.tar", 6, true)]
    [DataRow("TestData.rar", 3, true)]
    [DataRow("TestData.rar4", 3, true)]
    [DataRow("TestData.tar.bz2", 6, true)]
    [DataRow("TestData.tar.gz", 6, true)]
    [DataRow("TestData.tar.xz", 3, true)]
    [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8, true)]
    [DataRow("TestData.a", 3, true)]
    [DataRow("TestData.bsd.ar", 3, true)]
    [DataRow("TestData.iso", 3, true)]
    [DataRow("TestData.vhdx", 3, true)]
    [DataRow("TestData.wim", 3, true)]
    [DataRow("EmptyFile.txt", 1, true)]
    [DataRow("TestDataArchivesNested.Zip", 54, true)]
    [DataRow("TestDataArchivesNested.Zip", 54, false)]
    public async Task TimeoutTestAsync(string fileName, int expectedNumFiles = 3, bool parallel = false)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
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
            Assert.Fail();
        });
    }
}