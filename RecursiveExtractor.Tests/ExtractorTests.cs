// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Config;
using NLog.Targets;
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
        public void ExtractArchiveToDirectory(string fileName, int expectedNumFiles = 3)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var extractor = new Extractor();
            extractor.ExtractToDirectory(directory, path);
            var files = Array.Empty<string>();
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
        public async Task ExtractArchiveToDirectoryAsync(string fileName, int expectedNumFiles = 3)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var extractor = new Extractor();
            Assert.AreEqual(ExtractionStatusCode.OKAY, await extractor.ExtractToDirectoryAsync(directory, path));
            var files = Array.Empty<string>();
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
        [DataRow("TestDataArchivesNested.Zip", 51)]
        public void ExtractArchive(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions());
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

                
        [DataRow("TextFile.md")]
        [DataRow("Nested.zip", ".zip")]
        public void ExtractAsRaw(string fileName, string? RawExtension)
        {
            var extractor = new Extractor();
            var options = new ExtractorOptions()
            {
                RawExtensions = RawExtension is null ? null : new List<string>(){ RawExtension }
            };
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);

            var results = extractor.Extract(path, options);
            Assert.AreEqual(1, results.Count());
        }

        public static Dictionary<Regex, List<string>> TestArchivePasswords = new Dictionary<Regex, List<string>>()
        {
            {
                new Regex("EncryptedZipCrypto.zip"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TestData", // ZipCrypto Encrypted
                }
            },
            {
                new Regex("EncryptedAes.zip"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TestData"  // AES Encrypted
                }
            },
            {
                new Regex("\\.7z"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TestData", // TestDataEncrypted.7z
                    "TestData" // NestedEncrypted.7z
                }
            },
            {
                new Regex("\\.rar"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TestData"
                }
            }
        };

        [DataTestMethod]
        [DataRow("TestDataEncryptedZipCrypto.zip")]
        [DataRow("TestDataEncryptedAes.zip")]
        [DataRow("TestDataEncrypted.7z")]
        [DataRow("TestDataEncrypted.rar4")]
        // [DataRow("TestDataEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions()
            {
                Passwords = TestArchivePasswords
            }).ToList(); // Make this a list so it fully populates
            Assert.AreEqual(expectedNumFiles, results.Count);
        }

        [DataTestMethod]
        [DataRow("TestDataEncryptedZipCrypto.zip")]
        [DataRow("TestDataEncryptedAes.zip")]
        [DataRow("TestDataEncrypted.7z")]
        [DataRow("TestDataEncrypted.rar4")]
        // [DataRow("TestDataEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517

        public async Task ExtractEncryptedArchiveAsync(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.ExtractAsync(path, new ExtractorOptions()
            {
                Passwords = TestArchivePasswords
            });
            var numEntries = 0;
            await foreach(var entry in results)
            {
                numEntries++;
            }
            Assert.AreEqual(expectedNumFiles, numEntries);
        }

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
        [DataRow("TestDataArchivesNested.Zip", 51)]
        public async Task ExtractArchiveAsync(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.ExtractAsync(path, new ExtractorOptions());
            var numFound = 0;
            await foreach(var _ in results)
            {
                numFound++;
            }
            Assert.AreEqual(expectedNumFiles, numFound);
        }

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
        [DataRow("TestDataArchivesNested.Zip", 51)]
        public void ExtractArchiveParallel(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { Parallel = true });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

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
        [DataRow("TestDataArchivesNested.Zip", 51)]
        public async Task ExtractArchiveFromStreamAsync(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var results = extractor.ExtractAsync(path, stream, new ExtractorOptions());
            var numFiles = 0;
            await foreach (var result in results)
            {
                numFiles++;
            }
            Assert.AreEqual(expectedNumFiles, numFiles);
            stream.Close();
        }

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
        [DataRow("TestDataArchivesNested.Zip", 51)]
        public void ExtractArchiveFromStream(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var results = extractor.Extract(path, stream, new ExtractorOptions());
            Assert.AreEqual(expectedNumFiles, results.Count());
            stream.Close();
        }

        [DataTestMethod]
        [DataRow("TestData.zip", ArchiveFileType.ZIP)]
        [DataRow("TestData.7z", ArchiveFileType.P7ZIP)]
        [DataRow("TestData.Tar", ArchiveFileType.TAR)]
        [DataRow("TestData.rar", ArchiveFileType.RAR5)]
        [DataRow("TestData.rar4", ArchiveFileType.RAR)]
        [DataRow("TestData.tar.bz2", ArchiveFileType.BZIP2)]
        [DataRow("TestData.tar.gz", ArchiveFileType.GZIP)]
        [DataRow("TestData.tar.xz", ArchiveFileType.XZ)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", ArchiveFileType.DEB)]
        [DataRow("TestData.a", ArchiveFileType.AR)]
        [DataRow("TestData.iso", ArchiveFileType.ISO_9660)]
//        [DataRow("TestData.vhd", ArchiveFileType.VHD)]
        [DataRow("TestData.vhdx", ArchiveFileType.VHDX)]
        [DataRow("TestData.wim", ArchiveFileType.WIM)]
        [DataRow("Empty.vmdk", ArchiveFileType.VMDK)]
        [DataRow("EmptyFile.txt", ArchiveFileType.UNKNOWN)]
        public void TestMiniMagic(string fileName, ArchiveFileType expectedArchiveFileType)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            using var fs = new FileStream(path, FileMode.Open);
            // Test just based on the content
            var fileEntry = new FileEntry("NoName", fs);

            // We make sure the expected type matches and we have reset the stream
            Assert.AreEqual(expectedArchiveFileType, MiniMagic.DetectFileType(fileEntry));
            Assert.AreEqual(0, fileEntry.Content.Position);

            // Should also work if the stream doesn't start at 0
            fileEntry.Content.Position = 10;
            Assert.AreEqual(expectedArchiveFileType, MiniMagic.DetectFileType(fileEntry));
            Assert.AreEqual(10, fileEntry.Content.Position);
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
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
            IEnumerable<FileEntry> results;
            try
            {
                results = extractor.Extract(path, new ExtractorOptions()).ToList();
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

        [DataTestMethod]
        [DataRow("zip-slip-win.zip")]
        [DataRow("zip-slip-win.tar")]
        [DataRow("zip-slip.zip")]
        [DataRow("zip-slip.tar")]
        public void TestZipSlip(string fileName)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
            var results = extractor.Extract(path, new ExtractorOptions()).ToList();
            Assert.IsTrue(results.All(x => !x.FullPath.Contains("..")));
        }
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget
            {
                Name = "console",
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}",
            };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget, "*");

            LogManager.Configuration = config;
        }
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}