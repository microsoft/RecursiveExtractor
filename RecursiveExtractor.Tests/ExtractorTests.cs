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
        public void ExtractArchiveToDirectory(string fileName, int expectedNumFiles = 26)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
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
        public void ExtractArchiveToDirectoryAsync(string fileName, int expectedNumFiles = 26)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var extractor = new Extractor();
            extractor.ExtractToDirectoryAsync(directory, path);
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
            var results = extractor.Extract(path, new ExtractorOptions());
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        public static Dictionary<Regex, List<string>> TestArchivePasswords = new Dictionary<Regex, List<string>>()
        {
            {
                new Regex("Encrypted.zip"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TheMagicWordIsCelery", // ZipCrypto Encrypted
                }
            },
            {
                new Regex("EncryptedAES.zip"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TheMagicWordIsRadish"  // AES Encrypted
                }
            },
            {
                new Regex("\\.7z"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TheMagicWordIsTomato", // SharedEncrypted.7z
                    "TheMagicWordIsLettuce" // NestedEncrypted.7z
                }
            },
            {
                new Regex("\\.rar"),
                new List<string>()
                {
                    "AnIncorrectPassword",
                    "TheMagicWordIsPotato"
                }
            }
        };

        [DataTestMethod]
        [DataRow("SharedEncrypted.zip")]
        [DataRow("SharedEncryptedAES.zip")]
        [DataRow("SharedEncrypted.7z")]
        [DataRow("SharedEncrypted.rar4")]
        [DataRow("NestedEncrypted.7z", 26*3)]
        // [DataRow("SharedEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.Extract(path, new ExtractorOptions()
            {
                Passwords = TestArchivePasswords
            }).ToList(); // Make this a list so it fully populates
            Assert.AreEqual(expectedNumFiles, results.Count);
        }

        [DataTestMethod]
        [DataRow("SharedEncrypted.zip")]
        [DataRow("SharedEncryptedAES.zip")]
        [DataRow("SharedEncrypted.7z")]
        [DataRow("SharedEncrypted.rar4")]
        [DataRow("NestedEncrypted.7z", 26 * 3)]
        // [DataRow("SharedEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517

        public async Task ExtractEncryptedArchiveAsync(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
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
            var results = extractor.ExtractAsync(path, new ExtractorOptions());
            var numFound = 0;
            await foreach(var _ in results)
            {
                numFound++;
            }
            Assert.AreEqual(expectedNumFiles, numFound);
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
            var results = extractor.Extract(path, new ExtractorOptions() { Parallel = true });
            Assert.AreEqual(expectedNumFiles, results.Count());
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
            var results = extractor.Extract(path, stream, new ExtractorOptions());
            Assert.AreEqual(expectedNumFiles, results.Count());
            stream.Close();
        }

        [DataTestMethod]
        [DataRow("Shared.zip", ArchiveFileType.ZIP)]
        [DataRow("Shared.7z", ArchiveFileType.P7ZIP)]
        [DataRow("Shared.Tar", ArchiveFileType.TAR)]
        [DataRow("Shared.rar", ArchiveFileType.RAR5)]
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
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
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
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
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