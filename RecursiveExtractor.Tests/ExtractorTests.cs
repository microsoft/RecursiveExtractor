// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Tests
{
    [TestClass]
    public class ExtractorTests
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
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, new ExtractorOptions()).ToList();
            Assert.IsTrue(results.Count() == expectedNumFiles);
        }

        [DataTestMethod]
        [DataRow("SharedEncrypted.zip")]
        [DataRow("SharedEncrypted.7z")]
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, new ExtractorOptions()
            {
                Passwords = new Dictionary<System.Text.RegularExpressions.Regex, List<string>>()
                {
                    {
                        new Regex(".*"), 
                        new List<string>()
                        {
                            "AnIncorrectPassword",
                            "TheMagicWordIsPotato"
                        } 
                    } 
                }
            }).ToList();
            Assert.IsTrue(results.Count() == expectedNumFiles);
        }

        [DataTestMethod]
        [DataRow("SharedEncrypted.zip")]
        [DataRow("SharedEncrypted.7z")]
        public async Task ExtractEncryptedArchiveAsync(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFileAsync(path, new ExtractorOptions()
            {
                Passwords = new Dictionary<Regex, List<string>>()
                {
                    {
                        new Regex(".*"),
                        new List<string>()
                        {
                            "AnIncorrectPassword",
                            "TheMagicWordIsPotato"
                        }
                    }
                }
            });
            var numEntries = 0;
            await foreach(var entry in results)
            {
                numEntries++;
            }
            Assert.IsTrue(numEntries == expectedNumFiles);
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
        [DataRow("Shared.deb", 27)]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 1)]
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public async Task ExtractArchiveAsync(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFileAsync(path, new ExtractorOptions());
            var numFound = 0;
            await foreach(var _ in results)
            {
                numFound++;
            }
            Assert.IsTrue(numFound == expectedNumFiles);
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
        [DataRow("Shared.deb", 27)]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 1)]
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public void ExtractArchiveParallel(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, new ExtractorOptions() { Parallel = true }).ToList();
            Assert.IsTrue(results.Count() == expectedNumFiles);
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
        [DataRow("Shared.deb", 27)]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 1)]
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public async Task ExtractArchiveFromStreamAsync(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var results = extractor.ExtractStreamAsync(path, stream, new ExtractorOptions());
            var numFiles = 0;
            await foreach (var result in results)
            {
                numFiles++;
            }
            Assert.AreEqual(expectedNumFiles, numFiles);
            stream.Close();
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
        [DataRow("Shared.deb", 27)]
        [DataRow("Shared.ar")]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 29)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0)]
        [DataRow("TextFile.md", 1)]
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public void ExtractArchiveFromStream(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var results = extractor.ExtractStream(path, stream, new ExtractorOptions()).ToList();
            Assert.AreEqual(expectedNumFiles, results.Count);
            stream.Close();
        }

        [DataTestMethod]
        [DataRow("Shared.zip", ArchiveFileType.ZIP)]
        [DataRow("Shared.7z", ArchiveFileType.P7ZIP)]
        [DataRow("Shared.Tar", ArchiveFileType.TAR)]
        [DataRow("Shared.rar", ArchiveFileType.RAR)]
        [DataRow("Shared.rar4", ArchiveFileType.RAR)]
        [DataRow("Shared.tar.bz2", ArchiveFileType.BZIP2)]
        [DataRow("Shared.tar.gz", ArchiveFileType.GZIP)]
        [DataRow("Shared.tar.xz", ArchiveFileType.XZ)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", ArchiveFileType.DEB)]
        [DataRow("Shared.a", ArchiveFileType.UNKNOWN)]
        [DataRow("Shared.deb", ArchiveFileType.DEB)]
        [DataRow("Shared.ar", ArchiveFileType.AR)]
        [DataRow("Shared.iso", ArchiveFileType.ISO_9660)]
        [DataRow("Shared.vhd", ArchiveFileType.VHD)]
        [DataRow("Shared.vhdx", ArchiveFileType.VHDX)]
        [DataRow("Shared.wim", ArchiveFileType.WIM)]
        [DataRow("Empty.vmdk", ArchiveFileType.VMDK)]
        [DataRow("TextFile.md", ArchiveFileType.UNKNOWN)]
        public void TestMiniMagic(string fileName, ArchiveFileType expectedArchiveFileType)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using var fs = new FileStream(path, FileMode.Open);
            // Test just based on the content
            var fileEntry = new FileEntry("NoName", fs);

            Assert.IsTrue(MiniMagic.DetectFileType(fileEntry) == expectedArchiveFileType);
            Assert.IsTrue(fileEntry.Content.Position == 0);

            // Should also work if the stream doesn't start at 0
            fileEntry.Content.Position = 10;
            Assert.IsTrue(MiniMagic.DetectFileType(fileEntry) == expectedArchiveFileType);
            Assert.IsTrue(fileEntry.Content.Position == 10);

            // We should also detect just on file names if the content doesn't match
            var nameOnlyEntry = new FileEntry(fileName, new MemoryStream(), null, true);
            Assert.IsTrue(MiniMagic.DetectFileType(nameOnlyEntry) == expectedArchiveFileType);
        }

        [DataTestMethod]
        [DataRow("droste.zip")]
        [DataRow("10GB.7z.bz2")]
        [DataRow("10GB.gz.bz2")]
        [DataRow("10GB.rar.bz2")]
        [DataRow("10GB.xz.bz2")]
        [DataRow("10GB.zip.bz2")]
        [DataRow("zblg.zip")]
        [DataRow("zbsm.zip")]
        [DataRow("zbxl.zip")]
        public void TestQuineBombs(string fileName)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            IEnumerable<FileEntry> results;
            try
            {
                results = extractor.ExtractFile(path, new ExtractorOptions()).ToList();
                // Getting here means we didnt catch the bomb
            }
            // We should throw an overflow exception when we detect a quine or bomb
            catch (Exception e) when (
                    e is OverflowException)
            {
                return;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Shouldn't hit other exceptions in this test.");
            }
            // Getting here means we didnt catch the bomb
            Assert.Fail();
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}