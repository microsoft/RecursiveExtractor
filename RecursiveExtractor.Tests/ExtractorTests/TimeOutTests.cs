using Microsoft.CST.RecursiveExtractor;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class TimeOutTests
{
    /// <summary>
    /// Test data for timeout tests. WIM is Windows-only so conditionally included.
    /// </summary>
    public static TheoryData<string, bool> TimeoutData
    {
        get
        {
            var data = new TheoryData<string, bool>
            {
                { "TestData.7z", false },
                { "TestData.tar", false },
                { "TestData.rar", false },
                { "TestData.rar4", false },
                { "TestData.tar.bz2", false },
                { "TestData.tar.gz", false },
                { "TestData.tar.xz", false },
                { "sysvbanner_1.0-17fakesync1_amd64.deb", false },
                { "TestData.a", false },
                { "TestData.bsd.ar", false },
                { "TestData.iso", false },
                { "TestData.vhdx", false },
                { "EmptyFile.txt", false },
                { "TestData.zip", true },
                { "TestData.7z", true },
                { "TestData.tar", true },
                { "TestData.rar", true },
                { "TestData.rar4", true },
                { "TestData.tar.bz2", true },
                { "TestData.tar.gz", true },
                { "TestData.tar.xz", true },
                { "sysvbanner_1.0-17fakesync1_amd64.deb", true },
                { "TestData.a", true },
                { "TestData.bsd.ar", true },
                { "TestData.iso", true },
                { "TestData.vhdx", true },
                { "EmptyFile.txt", true },
                { "TestDataArchivesNested.zip", true },
                { "TestDataArchivesNested.zip", false },
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                data.Add("TestData.wim", false);
                data.Add("TestData.wim", true);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TimeoutData))]
    public void TimeoutTest(string fileName, bool parallel)
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
    [MemberData(nameof(TimeoutData))]
    public async Task TimeoutTestAsync(string fileName, bool parallel)
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
