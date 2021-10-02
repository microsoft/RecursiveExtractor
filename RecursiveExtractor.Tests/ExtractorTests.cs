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
        [DataRow(ArchiveFileType.ZIP, new byte[4] { 0x50, 0x4B, 0x03, 0x04 }, DisplayName = "zip")]
        [DataRow(ArchiveFileType.GZIP, new byte[2] { 0x1F, 0x8B }, DisplayName = "gzip")]
        //[DataRow(ArchiveFileType.XZ, new byte[6] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, DisplayName = "xz")]
        [DataRow(ArchiveFileType.BZIP2, new byte[3] { 0x42, 0x5A, 0x68 }, DisplayName = "bzip2")]
        [DataRow(ArchiveFileType.RAR, new byte[7] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, DisplayName = "rar")]
        [DataRow(ArchiveFileType.RAR5, new byte[8] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, DisplayName = "rar5")]
        [DataRow(ArchiveFileType.P7ZIP, new byte[6] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, DisplayName = "p7zip")]
        [DataRow(ArchiveFileType.WIM, new byte[5] { 0x4D, 0x53, 0x57, 0x49, 0x4D }, DisplayName = "wim")]
        [DataRow(ArchiveFileType.VMDK, new byte[4] { 0x4B, 0x44, 0x4D, 0x56 }, 512, "# Disk DescriptorFile", DisplayName = "vmdk")]
        //[DataRow(ArchiveFileType.DEB, new byte[7] { 0x21, 0x3c, 0x61, 0x72, 0x63, 0x68, 0x3e }, 68, "2.0\n000000000000000000000000000000000000000000000000000000009999", DisplayName = "deb")]
        //[DataRow(ArchiveFileType.AR, new byte[7] { 0x21, 0x3c, 0x61, 0x72, 0x63, 0x68, 0x3e }, 8, "                                                1         `\n", DisplayName = "ar")] // GnuArExtractor is still able to find entries, even with a minimal file header.
        [DataRow(ArchiveFileType.VHDX, new byte[8] { 0x76, 0x68, 0x64, 0x78, 0x66, 0x69, 0x6C, 0x65 }, DisplayName = "vhdx")]
        [DataRow(ArchiveFileType.TAR, new byte[1] { 0x00 }, 257, null, new byte[5] { 0x75, 0x73, 0x74, 0x61, 0x72 }, DisplayName = "tar")]
        [DataRow(ArchiveFileType.ISO_9660, new byte[1] { 0x00 }, 32769, "CD001", null, 2048, DisplayName = "iso")]
        [DataRow(ArchiveFileType.VHD, new byte[1] { 0x00 }, 512, null, new byte[] { 0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78 }, 0x200 - 8, DisplayName = "vhd")]

        public void FileTypeSetCorrectlyForFailingArchives(ArchiveFileType expectedArchiveType, byte[] header, int footerPosition = 0, string? footer = null, byte[]? footerBytes = null, int padding = 0)
        {
            var extractor = new Extractor();
            var fileData = new byte[9];
            Buffer.BlockCopy(header, 0, fileData, 0, header.Length);
            using var ms = new MemoryStream();
            ms.Write(fileData, 0, fileData.Length);
            var footerData = !string.IsNullOrEmpty(footer)
                    ? System.Text.Encoding.ASCII.GetBytes(footer)
                    : footerBytes;
            if (footerData != null)
            {
                ms.Position = footerPosition;
                ms.Write(footerData, 0, footerData.Length);

                if (padding > 0)
                {
                    ms.Write(new byte[padding], 0, padding);
                }
            }

            var fileEntry = new FileEntry(expectedArchiveType.ToString(), ms);

            Assert.AreEqual(expectedArchiveType, fileEntry.ArchiveType);

            var results = extractor.Extract(fileEntry, new ExtractorOptions() { ExtractSelfOnFail = true });
            Assert.AreEqual(1, results.Count());
            Assert.AreEqual(FileEntryStatus.FailedArchive, results.First().EntryStatus);
        }

        [DataTestMethod]
        [DataRow(ArchiveFileType.ZIP, new byte[4] { 0x50, 0x4B, 0x03, 0x04 }, DisplayName = "zip")]
        [DataRow(ArchiveFileType.GZIP, new byte[2] { 0x1F, 0x8B }, DisplayName = "gzip")]
        //[DataRow(ArchiveFileType.XZ, new byte[6] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, DisplayName = "xz")]
        [DataRow(ArchiveFileType.BZIP2, new byte[3] { 0x42, 0x5A, 0x68 }, DisplayName = "bzip2")]
        [DataRow(ArchiveFileType.RAR, new byte[7] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, DisplayName = "rar")]
        [DataRow(ArchiveFileType.RAR5, new byte[8] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, DisplayName = "rar5")]
        [DataRow(ArchiveFileType.P7ZIP, new byte[6] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, DisplayName = "p7zip")]
        [DataRow(ArchiveFileType.WIM, new byte[5] { 0x4D, 0x53, 0x57, 0x49, 0x4D }, DisplayName = "wim")]
        [DataRow(ArchiveFileType.VMDK, new byte[4] { 0x4B, 0x44, 0x4D, 0x56 }, 512, "# Disk DescriptorFile", DisplayName = "vmdk")]
        //[DataRow(ArchiveFileType.DEB, new byte[7] { 0x21, 0x3c, 0x61, 0x72, 0x63, 0x68, 0x3e }, 68, "2.0\n", DisplayName = "deb")]
        //[DataRow(ArchiveFileType.AR, new byte[7] { 0x21, 0x3c, 0x61, 0x72, 0x63, 0x68, 0x3e }, 8, "                                                1         `\n", DisplayName = "ar")] // GnuArExtractor is still able to find entries, even with a minimal file header.
        [DataRow(ArchiveFileType.VHDX, new byte[8] { 0x76, 0x68, 0x64, 0x78, 0x66, 0x69, 0x6C, 0x65 }, DisplayName = "vhdx")]
        [DataRow(ArchiveFileType.TAR, new byte[1] { 0x00 }, 257, null, new byte[5] { 0x75, 0x73, 0x74, 0x61, 0x72 }, DisplayName = "tar")]
        [DataRow(ArchiveFileType.ISO_9660, new byte[1] { 0x00 }, 32769, "CD001", null, 2048, DisplayName = "iso")]
        [DataRow(ArchiveFileType.VHD, new byte[1] { 0x00 }, 512, null, new byte[] { 0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78 }, 0x200 - 8, DisplayName = "vhd")]

        public async Task FileTypeSetCorrectlyForFailingArchivesAsync(ArchiveFileType expectedArchiveType, byte[] header, int footerPosition = 0, string? footer = null, byte[]? footerBytes = null, int padding = 0)
        {
            var extractor = new Extractor();
            var fileData = new byte[9];
            Buffer.BlockCopy(header, 0, fileData, 0, header.Length);
            using var ms = new MemoryStream();
            ms.Write(fileData, 0, fileData.Length);
            var footerData = !string.IsNullOrEmpty(footer)
                ? System.Text.Encoding.ASCII.GetBytes(footer)
                : footerBytes;
            if (footerData != null)
            {
                ms.Position = footerPosition;

                ms.Write(footerData, 0, footerData.Length);

                if (padding > 0)
                {
                    ms.Write(new byte[padding], 0, padding);
                }
            }

            var fileEntry = new FileEntry(expectedArchiveType.ToString(), ms);

            Assert.AreEqual(expectedArchiveType, fileEntry.ArchiveType);

            var list = await extractor.ExtractAsync(fileEntry, new ExtractorOptions() { ExtractSelfOnFail = true }).ToListAsync();
            
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(FileEntryStatus.FailedArchive, list[0].EntryStatus);
        }

        [DataTestMethod]
        [DataRow("TestDataEncryptedZipCrypto.zip")]
        [DataRow("TestDataEncryptedAes.zip")]
        [DataRow("TestDataEncrypted.7z")]
        [DataRow("TestDataEncrypted.rar4")]
        [DataRow("TestDataEncrypted.rar")]
        public void FileTypeSetCorrectlyForEncryptedArchives(string fileName, int expectedNumFiles = 1)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions());
            Assert.AreEqual(expectedNumFiles, results.Count());
            Assert.AreEqual(FileEntryStatus.EncryptedArchive, results.First().EntryStatus);
        }

        [DataTestMethod]
        [DataRow("TestDataEncryptedZipCrypto.zip")]
        [DataRow("TestDataEncryptedAes.zip")]
        [DataRow("TestDataEncrypted.7z")]
        [DataRow("TestDataEncrypted.rar4")]
        [DataRow("TestDataEncrypted.rar")]
        public async Task FileTypeSetCorrectlyForEncryptedArchivesAsync(string fileName, int expectedNumFiles = 1)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = new List<FileEntry>();
            await foreach (var entry in extractor.ExtractAsync(path, new ExtractorOptions()))
            {
                results.Add(entry);
            }
            Assert.AreEqual(expectedNumFiles, results.Count);
            Assert.AreEqual(FileEntryStatus.EncryptedArchive, results.First().EntryStatus);
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
            Assert.AreEqual(ExtractionStatusCode.Ok, await extractor.ExtractToDirectoryAsync(directory, path).ConfigureAwait(false));
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
        [DataRow("TestData.tar.bz2", 1)]
        [DataRow("TestData.tar.gz", 1)]
        [DataRow("TestData.tar.xz", 1)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 2)]
        [DataRow("TestData.a")]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 14)]
        public async Task ExtractArchiveAsyncNoRecursion(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var numResults = 0;
            await foreach (var result in extractor.ExtractAsync(path, new ExtractorOptions() { Recurse = false }))
            {
                numResults++;
            }
            Assert.AreEqual(expectedNumFiles, numResults);
        }

        [DataTestMethod]
        [DataRow("TestData.zip", 5)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 5)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 1)]
        [DataRow("TestData.tar.gz", 1)]
        [DataRow("TestData.tar.xz", 1)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 2)]
        [DataRow("TestData.a")]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 14)]
        public void ExtractArchiveParallelNoRecursion(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { Parallel = true, Recurse = false });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        [DataTestMethod]
        [DataRow("TestData.zip", 5)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 5)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 1)]
        [DataRow("TestData.tar.gz", 1)]
        [DataRow("TestData.tar.xz", 1)]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 2)]
        [DataRow("TestData.a")]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 14)]
        public void ExtractArchiveNoRecursion(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { Recurse = false });
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
        public void ExtractArchive(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions());
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
        public void ExtractArchiveParallel(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { Parallel = true });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        [DataTestMethod]
        [DataRow("TestData.zip")]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar")]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2")]
        [DataRow("TestData.tar.gz")]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
        [DataRow("TestData.a", 0)]
        [DataRow("TestData.ar", 0)]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 0)]
        [DataRow("TestDataArchivesNested.Zip", 9)]
        public async Task ExtractArchiveAsyncAllowFiltered(string fileName, int expectedNumFiles = 1)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.ExtractAsync(path, new ExtractorOptions() { AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
            var numResults = 0;
            await foreach (var result in results)
            {
                numResults++;
            }
            Assert.AreEqual(expectedNumFiles, numResults);
        }

        [DataTestMethod]
        [DataRow("TestData.zip")]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar")]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2")]
        [DataRow("TestData.tar.gz")]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
        [DataRow("TestData.a", 0)]
        [DataRow("TestData.ar", 0)]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 0)]
        [DataRow("TestDataArchivesNested.Zip", 9)]
        public void ExtractArchiveAllowFiltered(string fileName, int expectedNumFiles = 1)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        [DataTestMethod]
        [DataRow("TestData.zip")]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar")]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2")]
        [DataRow("TestData.tar.gz")]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 0)]
        [DataRow("TestData.a", 0)]
        [DataRow("TestData.ar", 0)]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 0)]
        [DataRow("TestDataArchivesNested.Zip", 9)]
        public void ExtractArchiveParallelAllowFiltered(string fileName, int expectedNumFiles = 1)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { Parallel = true, AllowFilters = new string[] { "**/Bar/**", "**/TestData.tar" } });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        [DataTestMethod]
        [DataRow("TestData.zip", 4)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 4)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 4)]
        [DataRow("TestData.tar.gz", 4)]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("TestData.a", 3)]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 42)]
        public void ExtractArchiveDenyFiltered(string fileName, int expectedNumFiles = 2)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { DenyFilters = new string[] { "**/Bar/**" } });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        [DataTestMethod]
        [DataRow("TestData.zip", 4)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 4)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 4)]
        [DataRow("TestData.tar.gz", 4)]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("TestData.a", 3)]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 42)]
        public void ExtractArchiveParallelDenyFiltered(string fileName, int expectedNumFiles = 2)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions() { Parallel = true, DenyFilters = new string[] { "**/Bar/**" } });
            Assert.AreEqual(expectedNumFiles, results.Count());
        }

        [DataTestMethod]
        [DataRow("TestData.zip", 4)]
        [DataRow("TestData.7z")]
        [DataRow("TestData.tar", 4)]
        [DataRow("TestData.rar")]
        [DataRow("TestData.rar4")]
        [DataRow("TestData.tar.bz2", 4)]
        [DataRow("TestData.tar.gz", 4)]
        [DataRow("TestData.tar.xz")]
        [DataRow("sysvbanner_1.0-17fakesync1_amd64.deb", 8)]
        [DataRow("TestData.a", 3)]
        //[DataRow("TestData.ar")]
        [DataRow("TestData.iso")]
        [DataRow("TestData.vhdx")]
        [DataRow("TestData.wim")]
        [DataRow("EmptyFile.txt", 1)]
        [DataRow("TestDataArchivesNested.Zip", 42)]
        public async Task ExtractArchiveAsyncDenyFiltered(string fileName, int expectedNumFiles = 2)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.ExtractAsync(path, new ExtractorOptions() { DenyFilters = new string[] { "**/Bar/**" } });
            var numResults = 0;
            await foreach (var result in results)
            {
                numResults++;
            }
            Assert.AreEqual(expectedNumFiles, numResults);
        }

        [DataRow("TextFile.md")]
        [DataRow("Nested.zip", ".zip")]
        public void ExtractAsRaw(string fileName, string? RawExtension)
        {
            var extractor = new Extractor();
            var options = new ExtractorOptions()
            {
                RawExtensions = RawExtension is null ? new List<string>() : new List<string>() { RawExtension }
            };
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);

            var results = extractor.Extract(path, options);
            Assert.AreEqual(1, results.Count());
        }

        public static Dictionary<Regex, List<string>> TestArchivePasswords { get; } = new Dictionary<Regex, List<string>>()
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
        //[DataRow("TestDataEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517
        public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.Extract(path, new ExtractorOptions()
            {
                Passwords = TestArchivePasswords
            }).ToList(); // Make this a list so it fully populates
            Assert.AreEqual(expectedNumFiles, results.Count);
            Assert.AreEqual(0, results.Count(x => x.EntryStatus == FileEntryStatus.EncryptedArchive));
        }

        [DataTestMethod]
        [DataRow("TestDataEncryptedZipCrypto.zip")]
        [DataRow("TestDataEncryptedAes.zip")]
        [DataRow("TestDataEncrypted.7z")]
        [DataRow("TestDataEncrypted.rar4")]
        //[DataRow("TestDataEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517

        public async Task ExtractEncryptedArchiveAsync(string fileName, int expectedNumFiles = 3)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
            var results = extractor.ExtractAsync(path, new ExtractorOptions()
            {
                Passwords = TestArchivePasswords
            });
            var numEntries = 0;
            var numEntriesEncrypted = 0;
            await foreach (var entry in results)
            {
                numEntries++;
                if (entry.EntryStatus == FileEntryStatus.EncryptedArchive)
                {
                    numEntriesEncrypted++;
                }
            }
            Assert.AreEqual(expectedNumFiles, numEntries);
            Assert.AreEqual(0, numEntriesEncrypted);
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
            var files = 0;
            await foreach (var res in results)
            {
                numFound++;
                if (res.EntryStatus == FileEntryStatus.Default)
                {
                    files++;
                }
            }
            Assert.AreEqual(expectedNumFiles, numFound);
            Assert.AreEqual(expectedNumFiles, files);
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
            Assert.AreEqual(expectedArchiveFileType, fileEntry.ArchiveType);
            Assert.AreEqual(0, fileEntry.Content.Position);

            // Should also work if the stream doesn't start at 0
            fileEntry.Content.Position = 10;
            Assert.AreEqual(expectedArchiveFileType, fileEntry.ArchiveType);
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
        [ExpectedException(typeof(OverflowException))]
        public void TestQuineBombs(string fileName)
        {
            var extractor = new Extractor();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Bombs", fileName);
            _ = extractor.Extract(path, new ExtractorOptions() { MemoryStreamCutoff = 1024 * 1024 * 1024 }).ToList();
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