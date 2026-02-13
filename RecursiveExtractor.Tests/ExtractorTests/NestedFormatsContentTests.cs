// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests
{
    /// <summary>
    /// Tests that validate content integrity through deeply nested extraction
    /// across multiple new archive formats (ZIP → ACE → ARC → ARJ → TAR).
    /// </summary>
    public class NestedFormatsContentTests
    {
        private const string NestedArchiveFileName = "NestedFormatsTest.zip";
        private const string File1ExpectedContent = "Hello from File1. This is a test file for nested archive extraction.\n";
        private const string File2ExpectedContent = "Greetings from File2. This verifies content integrity after recursive extraction.\n";

        private string GetArchivePath() =>
            Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", NestedArchiveFileName);

        [Fact]
        public void ExtractNestedFormatsContentSync()
        {
            var extractor = new Extractor();
            var path = GetArchivePath();
            var results = extractor.Extract(path, new ExtractorOptions()).ToList();

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, r => r.EntryStatus == FileEntryStatus.FailedArchive);

            var file1 = results.FirstOrDefault(r => r.FullPath.EndsWith("file1.txt"));
            var file2 = results.FirstOrDefault(r => r.FullPath.EndsWith("file2.txt"));

            Assert.NotNull(file1);
            Assert.NotNull(file2);

            using var reader1 = new StreamReader(file1.Content);
            Assert.Equal(File1ExpectedContent, reader1.ReadToEnd());

            using var reader2 = new StreamReader(file2.Content);
            Assert.Equal(File2ExpectedContent, reader2.ReadToEnd());
        }

        [Fact]
        public async Task ExtractNestedFormatsContentAsync()
        {
            var extractor = new Extractor();
            var path = GetArchivePath();
            var results = await extractor.ExtractAsync(path, new ExtractorOptions()).ToListAsync();

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, r => r.EntryStatus == FileEntryStatus.FailedArchive);

            var file1 = results.FirstOrDefault(r => r.FullPath.EndsWith("file1.txt"));
            var file2 = results.FirstOrDefault(r => r.FullPath.EndsWith("file2.txt"));

            Assert.NotNull(file1);
            Assert.NotNull(file2);

            using var reader1 = new StreamReader(file1.Content);
            Assert.Equal(File1ExpectedContent, reader1.ReadToEnd());

            using var reader2 = new StreamReader(file2.Content);
            Assert.Equal(File2ExpectedContent, reader2.ReadToEnd());
        }
    }
}
