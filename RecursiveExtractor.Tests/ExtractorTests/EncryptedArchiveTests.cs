using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class EncryptedArchiveTests : BaseExtractorTestClass
{
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
        var results = extractor.Extract(path, new ExtractorOptions() { Parallel = false }).ToList();
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
    [DataRow("TestDataEncryptedZipCrypto.zip")]
    [DataRow("TestDataEncryptedAes.zip")]
    [DataRow("TestDataEncrypted.7z")]
    [DataRow("TestDataEncrypted.rar4")]
    [DataRow("EncryptedWithPlainNames.7z", 1)]
    [DataRow("EncryptedWithPlainNames.rar4", 1)]
    //[DataRow("TestDataEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517
    public void ExtractEncryptedArchive(string fileName, int expectedNumFiles = 3)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.Extract(path, new ExtractorOptions() { Passwords = TestArchivePasswords })
            .ToList(); // Make this a list so it fully populates
        Assert.AreEqual(expectedNumFiles, results.Count);
        Assert.AreEqual(0, results.Count(x => x.EntryStatus == FileEntryStatus.EncryptedArchive));
    }

    [DataTestMethod]
    [DataRow("TestDataEncryptedZipCrypto.zip")]
    [DataRow("TestDataEncryptedAes.zip")]
    [DataRow("TestDataEncrypted.7z")]
    [DataRow("TestDataEncrypted.rar4")]
    [DataRow("EncryptedWithPlainNames.7z", 1)]
    [DataRow("EncryptedWithPlainNames.rar4", 1)]
    //[DataRow("TestDataEncrypted.rar")] // RAR5 is not yet supported by SharpCompress: https://github.com/adamhathcock/sharpcompress/issues/517
    public async Task ExtractEncryptedArchiveAsync(string fileName, int expectedNumFiles = 3)
    {
        var extractor = new Extractor();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        var results = extractor.ExtractAsync(path, new ExtractorOptions() { Passwords = TestArchivePasswords });
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


    public static Dictionary<Regex, List<string>> TestArchivePasswords { get; } = new Dictionary<Regex, List<string>>()
    {
        {
            new Regex("EncryptedZipCrypto.zip"), new List<string>()
            {
                "AnIncorrectPassword", "TestData", // ZipCrypto Encrypted
            }
        },
        {
            new Regex("EncryptedAes.zip"), new List<string>()
            {
                "AnIncorrectPassword", "TestData" // AES Encrypted
            }
        },
        {
            new Regex("\\.7z"), new List<string>()
            {
                "AnIncorrectPassword",
                "TestData", // TestDataEncrypted.7z
                "TestData", // NestedEncrypted.7z
                "asdf" // EncryptedWithPlainNames.7z
            }
        },
        { new Regex("\\.rar"), new List<string>() { "AnIncorrectPassword", "TestData", "asdf" } }
    };
}