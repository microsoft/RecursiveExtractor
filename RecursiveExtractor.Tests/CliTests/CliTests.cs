// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Cli;
using Microsoft.CST.RecursiveExtractor.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecursiveExtractor.Tests.ExtractorTests;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace RecursiveExtractor.Tests.CliTests
{
    [TestClass]
    public class CliTests
    {
        [DataTestMethod]
        [DataRow("TestData.zip", 5)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 6)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 6)]
        [DataRow("TestData.tar.gz", 6)]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("TestData.a")]
        [DataRow("TestData.bsd.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 54)]
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
                Directory.Delete(directory, true);
            }
            Assert.AreEqual(expectedNumFiles, files.Length);
        }
        
        [DataTestMethod]
        [DataRow("TestData.zip", 5)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 6)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 6)]
        [DataRow("TestData.tar.gz", 6)]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("TestData.a")]
        [DataRow("TestData.bsd.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 54)]
        public void ExtractArchiveSingleThread(string fileName, int expectedNumFiles = 3)
        {
            ExtractArchive(fileName, expectedNumFiles, true);
        }

        [DataTestMethod]
        [DataRow("TestDataForFilters.7z")]
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
                Directory.Delete(directory, true);
            }
            Assert.AreEqual(expectedNumFiles, files.Length);
        }

        [DataTestMethod]
        [DataRow("TestDataForFilters.7z")]
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
                Directory.Delete(directory, true);
            }
            Assert.AreEqual(expectedNumFiles, files.Length);
        }

        [DataTestMethod]
        [DataRow("TestDataEncrypted.7z")]
        [DataRow("TestDataEncryptedAes.zip")]
        [DataRow("TestDataEncrypted.rar4")]
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 3)
        {
            var directory = TestPathHelpers.GetFreshTestDirectory();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var passwords = EncryptedArchiveTests.TestArchivePasswords.Values.SelectMany(x => x);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true, Passwords = passwords });
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
                Directory.Delete(directory, true);
            }
            Assert.AreEqual(expectedNumFiles, files.Length);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            TestPathHelpers.DeleteTestDirectory();
        }
        
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}