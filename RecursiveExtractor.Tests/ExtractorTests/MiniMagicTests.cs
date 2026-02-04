using Microsoft.CST.RecursiveExtractor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class MiniMagicTests : BaseExtractorTestClass
{
    [TestMethod]
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
    [DataRow("UdfTest.iso", ArchiveFileType.UDF)]
    [DataRow("TestData.vhdx", ArchiveFileType.VHDX)]
    [DataRow("TestData.wim", ArchiveFileType.WIM)]
    [DataRow("Empty.vmdk", ArchiveFileType.VMDK)]
    [DataRow("HfsSampleUDCO.dmg", ArchiveFileType.DMG)]
    [DataRow("TestData.arj", ArchiveFileType.ARJ)]
    [DataRow("TestData.arc", ArchiveFileType.ARC)]
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
}