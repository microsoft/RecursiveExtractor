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
        [DataRow("Shared.zip")]
        [DataRow("Shared.7z")]
        [DataRow("Shared.Tar")]
        [DataRow("Shared.rar")]
        [DataRow("Shared.rar4")]
        [DataRow("Shared.tar.bz2")]
        [DataRow("Shared.tar.gz")]
        [DataRow("Shared.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("Shared.a", 1)]
        [DataRow("Shared.deb", 27)]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 1)]
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public void ExtractArchive(string fileName, int expectedNumFiles = 26)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
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
        [DataRow("Shared.zip")]
        [DataRow("Shared.7z")]
        [DataRow("Shared.Tar")]
        [DataRow("Shared.rar")]
        [DataRow("Shared.rar4")]
        [DataRow("Shared.tar.bz2")]
        [DataRow("Shared.tar.gz")]
        [DataRow("Shared.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
        [DataRow("Shared.a", 0)]
        [DataRow("Shared.deb")]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd")] // Filename formatting in the VHD has CS capitalized so nothing matches
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 0)]
        [DataRow("Nested.Zip", 22 * 8)]
        public void ExtractArchiveWithAllowFilters(string fileName, int expectedNumFiles = 22)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions()
            {
                Input = path,
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
        [DataRow("Shared.zip")]
        [DataRow("Shared.7z")]
        [DataRow("Shared.Tar")]
        [DataRow("Shared.rar")]
        [DataRow("Shared.rar4")]
        [DataRow("Shared.tar.bz2")]
        [DataRow("Shared.tar.gz")]
        [DataRow("Shared.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("Shared.a", 1)]
        [DataRow("Shared.deb", 5)]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 7)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 1)]
        [DataRow("Nested.Zip", 4 * 8 + 1)] // there's one extra metadata file in there
        public void ExtractArchiveWithDenyFilters(string fileName, int expectedNumFiles = 4)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            RecursiveExtractorClient.ExtractCommand(new ExtractCommandOptions()
            {
                Input = path,
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
        [DataRow("SharedEncrypted.7z")]
        [DataRow("SharedEncrypted.zip")]
        [DataRow("SharedEncrypted.rar4")]
        [DataRow("NestedEncrypted.7z", 26 * 3)] // there's one extra metadata file in there
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 26)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
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