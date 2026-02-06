// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Cli;
using Microsoft.CST.RecursiveExtractor.Tests;
using RecursiveExtractor.Tests.ExtractorTests;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace RecursiveExtractor.Tests.CliTests
{
    public class CliTests : BaseExtractorTestClass
    {
        [Theory]
        [InlineData("TestData.zip", 5)]
        [InlineData("TestData.7z")]
        [InlineData("TestData.tar", 6)]
        [InlineData("TestData.rar")]
        [InlineData("TestData.rar4")]
        [InlineData("TestData.tar.bz2", 6)]
        [InlineData("TestData.tar.gz", 6)]
        [InlineData("TestData.tar.xz")]
        [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [InlineData("TestData.a")]
        [InlineData("TestData.bsd.ar")]
        [InlineData("TestData.iso")]
        [InlineData("TestData.vhdx")]
        [InlineData("TestData.wim")]
        [InlineData("EmptyFile.txt", 1)]
        [InlineData("TestDataArchivesNested.Zip", 54)]
        public void ExtractArchiveParallel(string fileName, int expectedNumFiles = 3)
        {
            ExtractArchive(fileName, expectedNumFiles, false);
        }

        internal void ExtractArchive(string fileName, int expectedNumFiles, bool singleThread)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true, SingleThread = singleThread});
            var files = Array.Empty<string>();
            Thread.Sleep(100);
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }
        
        [Theory]
        [InlineData("TestData.zip", 5)]
        [InlineData("TestData.7z")]
        [InlineData("TestData.tar", 6)]
        [InlineData("TestData.rar")]
        [InlineData("TestData.rar4")]
        [InlineData("TestData.tar.bz2", 6)]
        [InlineData("TestData.tar.gz", 6)]
        [InlineData("TestData.tar.xz")]
        [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [InlineData("TestData.a")]
        [InlineData("TestData.bsd.ar")]
        [InlineData("TestData.iso")]
        [InlineData("TestData.vhdx")]
        [InlineData("TestData.wim")]
        [InlineData("EmptyFile.txt", 1)]
        [InlineData("TestDataArchivesNested.Zip", 54)]
        public void ExtractArchiveSingleThread(string fileName, int expectedNumFiles = 3)
        {
            ExtractArchive(fileName, expectedNumFiles, true);
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
                AllowFilters = new string[]
                {
                    "*.cs"
                }
            });
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
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
                DenyFilters = new string[]
                {
                    "*.cs"
                }
            });
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
            }
            Assert.Equal(expectedNumFiles, files.Length);
        }

        [Theory]
        [InlineData("TestDataEncrypted.7z")]
        [InlineData("TestDataEncryptedAes.zip")]
        [InlineData("TestDataEncrypted.rar4")]
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 3)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var passwords = EncryptedArchiveTests.TestArchivePasswords.Values.SelectMany(x => x);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true, Passwords = passwords });
            string[] files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
            Assert.Equal(expectedNumFiles, files.Length);
        }
        
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}