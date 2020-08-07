// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void ExtractArchive(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, new ExtractorOptions()).ToList();
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
        public async Task ExtractArchiveFromStreamAsync(string fileName, int expectedNumFiles = 26)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var results = extractor.ExtractStreamAsync(path, stream, new ExtractorOptions());
            var numFiles = 0;
            await foreach(var result in results)
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
        [DataRow("Shared.zip")]
        [DataRow("Shared.7z")]
        [DataRow("Shared.Tar")]
        [DataRow("Shared.rar")]
        [DataRow("Shared.rar4")]
        [DataRow("Shared.tar.bz2")]
        [DataRow("Shared.tar.gz")]
        [DataRow("Shared.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 3, 5)]
        [DataRow("Shared.a", 1, 0)]
        [DataRow("Shared.deb", 24, 3)]
        [DataRow("Shared.ar", 23, 3)]
        [DataRow("Shared.iso")]
        [DataRow("Shared.vhd", 24, 5)] // 26 + Some invisible system files
        [DataRow("Shared.vhdx")]
        [DataRow("Shared.wim")]
        [DataRow("Empty.vmdk", 0, 0)]
        [DataRow("TextFile.md", 1, 0)]
        public void TestPassFilter(string fileName, int expectedHighPass = 24, int expectedLowPass = 2)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            using var stream = new FileStream(path, FileMode.Open);
            var extractorOptions = new ExtractorOptions()
            {
                Filter = SizeGreaterThan1000
            };
            var extractorOptionsLambda = new ExtractorOptions()
            {
                Filter = (FileEntryInfo fei) => fei.Size <= 1000
            };
            var results = extractor.ExtractStream(path, stream, extractorOptions).ToList();
            var invertResults = extractor.ExtractStream(path, stream, extractorOptionsLambda).ToList();
            Assert.AreEqual(expectedHighPass, results.Count);
            Assert.IsTrue(results.All(x => x.Content.Length > 1000));
            Assert.AreEqual(expectedLowPass, invertResults.Count);
            Assert.IsTrue(invertResults.All(x => x.Content.Length <= 1000));
            stream.Close();
        }

        private bool SizeGreaterThan1000(FileEntryInfo fei)
        {
            return fei.Size > 1000;
        }

        [DataTestMethod]
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public void ExtractNestedArchive(string fileName, int expectedNumFiles)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            var results = extractor.ExtractFile(path, new ExtractorOptions());
            Assert.AreEqual(expectedNumFiles, results.Count());
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
            var nameOnlyEntry = new FileEntry(fileName, new MemoryStream());
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