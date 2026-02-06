// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests
{
    public class ExpectedNumFilesTests : BaseExtractorTestClass
    {
     
        /// <summary>
        /// Mapping from Test archive name to expected number of files to extract
        /// </summary>
        public static IEnumerable<object[]> ArchiveData
        {
            get
            {
                return new[]
                { 
                    new object[] { "100Trees.7z", 101 },
                    new object[] { "TestData.zip", 5 },
                    new object[] { "TestData.7z",3 },
                    new object[] { "TestData.tar", 6 },
                    new object[] { "TestData.rar",3 },
                    new object[] { "TestData.rar4",3 },
                    new object[] { "TestData.tar.bz2", 6 },
                    new object[] { "TestData.tar.gz", 6 },
                    new object[] { "TestData.tar.xz",3 },
                    new object[] { "sysvbanner_1.0-17fakesync1_amd64.deb", 8 },
                    new object[] { "TestData.a",3 },
                    new object[] { "TestData.bsd.ar",3 },
                    new object[] { "TestData.iso",3 },
                    new object[] { "TestData.vhdx",3 },
                    new object[] { "TestData.wim",3 },
                    new object[] { "EmptyFile.txt", 1 },
                    new object[] { "TestDataArchivesNested.Zip", 54 },
                    new object[] { "UdfTest.iso", 3 },
                    new object[] { "UdfTestWithMultiSystem.iso", 3 },
//                    new object[] { "HfsSampleUDCO.dmg", 2 }
                };
            }
        }
        
        /// <summary>
        /// Mapping from Test archive name to expected number of files to extract when recursion is disabled
        /// </summary>
        public static IEnumerable<object[]> NoRecursionData
        {
            get
            {
                return new[]
                { 
                    new object[] { "100Trees.7z", 101 },
                    new object[] { "TestData.zip", 5 },
                    new object[] { "TestData.7z", 3 },
                    new object[] { "TestData.tar", 6 },
                    new object[] { "TestData.rar", 3 },
                    new object[] { "TestData.rar4", 3 },
                    new object[] { "TestData.tar.bz2", 1 },
                    new object[] { "TestData.tar.gz", 1 },
                    new object[] { "TestData.tar.xz", 1 },
                    new object[] { "sysvbanner_1.0-17fakesync1_amd64.deb", 2 },
                    new object[] { "TestData.a", 3 },
                    new object[] { "TestData.bsd.ar", 3 },
                    new object[] { "TestData.iso", 3 },
                    new object[] { "TestData.vhdx", 3 },
                    new object[] { "TestData.wim", 3 },
                    new object[] { "EmptyFile.txt", 1 },
                    new object[] { "TestDataArchivesNested.Zip", 14 },
                    new object[] { "UdfTestWithMultiSystem.iso", 3 },
//                    new object[] { "HfsSampleUDCO.dmg", 2 }
                };
            }
        }

        private ExtractorOptions GetExtractorOptions(bool parallel = false)
        {
            return parallel ? defaultExtractorTestOptionsParallel : defaultExtractorTestOptions;
        }

        private ExtractorOptions defaultExtractorTestOptions = new ExtractorOptions() { MaxExtractedBytesRatio = 300 };
        private ExtractorOptions defaultExtractorTestOptionsParallel = new ExtractorOptions() { Parallel = true, MaxExtractedBytesRatio = 300 };
        
        [Theory]
        [MemberData(nameof(ArchiveData))]
        public void ExtractArchiveToDirectoryParallel(string fileName, int expectedNumFiles)
        {
            ExtractArchiveToDirectory(fileName, expectedNumFiles, true);
        }
        
        [Theory]
        [MemberData(nameof(ArchiveData))]
        public void ExtractArchiveToDirectorySingleThread(string fileName, int expectedNumFiles)
        {
            ExtractArchiveToDirectory(fileName, expectedNumFiles, false);
        }
        
        internal void ExtractArchiveToDirectory(string fileName, int expectedNumFiles, bool parallel)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var extractor = new Extractor();
            extractor.ExtractToDirectory(directory, path, GetExtractorOptions(parallel));
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }

        [Theory]
        [MemberData(nameof(ArchiveData))]
        public async Task ExtractArchiveToDirectoryAsync(string fileName, int expectedNumFiles)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var extractor = new Extractor();
            Assert.Equal(ExtractionStatusCode.Ok, await extractor.ExtractToDirectoryAsync(directory, path, GetExtractorOptions()));
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }

        [Theory]
        [MemberData(nameof(NoRecursionData))]
        public async Task ExtractArchiveAsyncNoRecursion(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var numResults = 0;
            var opts = GetExtractorOptions();
            opts.Recurse = false;
            await foreach (var result in extractor.ExtractAsync(path, opts))
            {
                numResults++;
            }
            Assert.Equal(expectedNumFiles, numResults);
        }

        [Theory]
        [MemberData(nameof(NoRecursionData))]
        public void ExtractArchiveParallelNoRecursion(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var opts = GetExtractorOptions(true);
            opts.Recurse = false;
            var results = extractor.Extract(path, opts);
            Assert.Equal(expectedNumFiles, results.Count(entry => entry.EntryStatus == FileEntryStatus.Default));
        }

        [Theory]
        [MemberData(nameof(NoRecursionData))]
        public void ExtractArchiveNoRecursion(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var opts = GetExtractorOptions();
            opts.Recurse = false;
            var results = extractor.Extract(path, opts);
            Assert.Equal(expectedNumFiles, results.Count(entry => entry.EntryStatus == FileEntryStatus.Default));
        }

        [Theory]
        [MemberData(nameof(ArchiveData))]
        public void ExtractArchive(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, GetExtractorOptions()).ToList();
            foreach (var result in results)
            {
                Assert.NotEqual(FileEntryStatus.FailedArchive, result.EntryStatus);
            }
            Assert.Equal(expectedNumFiles, results.Count);
        }
        
        [Theory]
        [MemberData(nameof(ArchiveData))]
        public void ExtractArchiveParallel(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, GetExtractorOptions(true)).ToList();
            var names = results.Select(x => x.FullPath);
            var stringOfNames = string.Join("\n", names);
            Assert.Equal(expectedNumFiles, results.Count());
        }
        
        [Theory]
        [MemberData(nameof(ArchiveData))]
        public async Task ExtractArchiveAsync(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.ExtractAsync(path, GetExtractorOptions());
            var numFound = 0;
            var files = 0;
            await foreach (var res in results)
            {
                numFound++;
                if (res.EntryStatus == FileEntryStatus.Default)
                {
                    files++;
                }
            }
            Assert.Equal(expectedNumFiles, numFound);
            Assert.Equal(expectedNumFiles, files);
        }

        [Theory]
        [MemberData(nameof(ArchiveData))]
        public async Task ExtractArchiveFromStreamAsync(string fileName, int expectedNumFiles)
        {
        var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var results = extractor.ExtractAsync(path, stream, new ExtractorOptions());
            var numFiles = 0;
            await foreach (var result in results)
            {
                numFiles++;
            }
            Assert.Equal(expectedNumFiles, numFiles);
            stream.Close();
        }

        [Theory]
        [MemberData(nameof(ArchiveData))]
        public void ExtractArchiveFromStream(string fileName, int expectedNumFiles)
        {
        var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var results = extractor.Extract(path, stream, GetExtractorOptions());
            Assert.Equal(expectedNumFiles, results.Count());
            stream.Close();
        }

        [Theory]
        [MemberData(nameof(ArchiveData))]        
        public void ExtractArchiveSmallBatchSize(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var opts = GetExtractorOptions(true);
            opts.BatchSize = 2;
            var results = extractor.Extract(path, opts);
            Assert.Equal(expectedNumFiles, results.Count(entry => entry.EntryStatus == FileEntryStatus.Default));
        }
    }
}