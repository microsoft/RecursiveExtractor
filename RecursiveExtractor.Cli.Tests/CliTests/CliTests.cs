// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Cli;
using Microsoft.CST.RecursiveExtractor.Tests;
using RecursiveExtractor.Tests.ExtractorTests;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace RecursiveExtractor.Tests.CliTests
{
    public class CliTests : IClassFixture<BaseExtractorTestClass>
    {
        /// <summary>
        /// Test data for CLI extraction tests. WIM is Windows-only so conditionally included.
        /// TestDataArchivesNested.zip count varies by platform because embedded WIM is only extracted on Windows.
        /// </summary>
        public static TheoryData<string, int> CliArchiveData
        {
            get
            {
                var data = new TheoryData<string, int>
                {
                    { "TestData.zip", 5 },
                    { "TestData.7z", 3 },
                    { "TestData.tar", 6 },
                    { "TestData.rar", 3 },
                    { "TestData.rar4", 3 },
                    { "TestData.tar.bz2", 6 },
                    { "TestData.tar.gz", 6 },
                    { "TestData.tar.xz", 3 },
                    { "sysvbanner_1.0-17fakesync1_amd64.deb", 8 },
                    { "TestData.a", 3 },
                    { "TestData.bsd.ar", 3 },
                    { "TestData.iso", 3 },
                    { "TestData.vhdx", 3 },
                    { "EmptyFile.txt", 1 },
                    { "TestDataArchivesNested.zip", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 54 : 52 },
                };
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    data.Add("TestData.wim", 3);
                }
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(CliArchiveData))]
        public void ExtractArchiveParallel(string fileName, int expectedNumFiles)
        {
            CliTests.ExtractArchive(fileName, expectedNumFiles, false);
        }

        internal static void ExtractArchive(string fileName, int expectedNumFiles, bool singleThread)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true, SingleThread = singleThread});
            var files = Array.Empty<string>();
            Thread.Sleep(100);
            if (Directory.Exists(directory))
            {
                files = [.. Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)];
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }
        
        [Theory]
        [MemberData(nameof(CliArchiveData))]
        public void ExtractArchiveSingleThread(string fileName, int expectedNumFiles)
        {
            CliTests.ExtractArchive(fileName, expectedNumFiles, true);
        }

        [Theory]
        [InlineData("TestDataForFilters.7z")]
        public void ExtractArchiveWithAllowFilters(string fileName, int expectedNumFiles = 1)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var newpath = TempPath.GetTempFilePath();
            File.Copy(path, newpath,true);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions()
            {
                Input = newpath,
                Output = directory,
                Verbose = true,
                AllowFilters =
                [
                    "*.cs"
                ]
            });
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = [.. Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)];
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }

        [Theory]
        [InlineData("TestDataForFilters.7z")]
        public void ExtractArchiveWithDenyFilters(string fileName, int expectedNumFiles = 2)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var newpath = TempPath.GetTempFilePath();
            File.Copy(path, newpath, true);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions()
            {
                Input = newpath,
                Output = directory,
                Verbose = true,
                DenyFilters =
                [
                    "*.cs"
                ]
            });
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = [.. Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)];
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }

        [Theory]
        [InlineData("TestDataEncrypted.7z")]
        [InlineData("TestDataEncryptedAES.zip")]
        [InlineData("TestDataEncrypted.rar4")]
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 3)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var passwords = EncryptedArchiveTests.TestArchivePasswords.Values.SelectMany(x => x);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true, Passwords = passwords });
            string[] files = [.. Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)];
            Assert.Equal(expectedNumFiles, files.Length);
        }        
    }
}