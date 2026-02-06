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
    [Collection(ExtractorTestCollection.Name)]
    public class ExpectedNumFilesTests
    {
     
        /// <summary>
        /// Mapping from Test archive name to expected number of files to extract
        /// </summary>
        public static TheoryData<string, int> ArchiveData
        {
            get
            {
                return new TheoryData<string, int>
                { 
                    { "100Trees.7z", 101 },
                    { "TestData.zip", 5 },
                    { "TestData.7z",3 },
                    { "TestData.tar", 6 },
                    { "TestData.rar",3 },
                    { "TestData.rar4",3 },
                    { "TestData.tar.bz2", 6 },
                    { "TestData.tar.gz", 6 },
                    { "TestData.tar.xz",3 },
                    { "sysvbanner_1.0-17fakesync1_amd64.deb", 8 },
                    { "TestData.a",3 },
                    { "TestData.bsd.ar",3 },
                    { "TestData.iso",3 },
                    { "TestData.vhdx",3 },
                    { "TestData.wim",3 },
                    { "EmptyFile.txt", 1 },
                    { "TestDataArchivesNested.Zip", 54 },
                    { "UdfTest.iso", 3 },
                    { "UdfTestWithMultiSystem.iso", 3 },
                    { "TestData.arj", 1 },
                    { "TestData.arc", 1 },
//                    { "HfsSampleUDCO.dmg", 2 }
                };
            }
        }
        
        /// <summary>
        /// Mapping from Test archive name to expected number of files to extract when recursion is disabled
        /// </summary>
        public static TheoryData<string, int> NoRecursionData
        {
            get
            {
                return new TheoryData<string, int>
                { 
                    { "100Trees.7z", 101 },
                    { "TestData.zip", 5 },
                    { "TestData.7z", 3 },
                    { "TestData.tar", 6 },
                    { "TestData.rar", 3 },
                    { "TestData.rar4", 3 },
                    { "TestData.tar.bz2", 1 },
                    { "TestData.tar.gz", 1 },
                    { "TestData.tar.xz", 1 },
                    { "sysvbanner_1.0-17fakesync1_amd64.deb", 2 },
                    { "TestData.a", 3 },
                    { "TestData.bsd.ar", 3 },
                    { "TestData.iso", 3 },
                    { "TestData.vhdx", 3 },
                    { "TestData.wim", 3 },
                    { "EmptyFile.txt", 1 },
                    { "TestDataArchivesNested.Zip", 14 },
                    { "UdfTestWithMultiSystem.iso", 3 },
                    { "TestData.arj", 1 },
                    { "TestData.arc", 1 },
//                    { "HfsSampleUDCO.dmg", 2 }
                };
            }
        }

        private ExtractorOptions GetExtractorOptions(bool parallel = false)
        {
            return parallel ? defaultExtractorTestOptionsParallel : defaultExtractorTestOptions;
        }

        private readonly ExtractorOptions defaultExtractorTestOptions = new() { MaxExtractedBytesRatio = 300 };
        private readonly ExtractorOptions defaultExtractorTestOptionsParallel = new() { Parallel = true, MaxExtractedBytesRatio = 300 };
        
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
            Assert.Equal(expectedNumFiles, results.Count);
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