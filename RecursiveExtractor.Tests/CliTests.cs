// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.RecursiveExtractor.Cli;

namespace Microsoft.CST.RecursiveExtractor.Tests
{
    [TestClass]
    public class ExtractorCliTests
    {
        [DataTestMethod]
        [DataRow("TestData.zip", 5)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 5)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 5)]
        [DataRow("TestData.tar.gz", 5)]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("TestData.a")]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 49)]
        public void ExtractArchive(string fileName, int expectedNumFiles = 3)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true });
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
        public void ExtractArchiveWithAllowFilters(string fileName, int expectedNumFiles = 1)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var newpath = Path.GetTempFileName();
            File.Copy(path, newpath,true);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions()
            {
                Input = newpath,
                Output = directory,
                Verbose = true,
                AllowFilters = new string[]
                {
                    ".[cC][sS]$"
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
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var newpath = Path.GetTempFileName();
            File.Copy(path, newpath, true);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions()
            {
                Input = newpath,
                Output = directory,
                Verbose = true,
                DenyFilters = new string[]
                {
                    ".[cC][sS]$"
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
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var passwords = ExtractorTests.TestArchivePasswords.Values.SelectMany(x => x);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true, Passwords = passwords });
            var files = Array.Empty<string>();
            if (Directory.Exists(directory))
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToArray();
                Directory.Delete(directory, true);
            }
            Assert.AreEqual(expectedNumFiles, files.Length);
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}