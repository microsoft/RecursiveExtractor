using Microsoft.CST.RecursiveExtractor;
using System.IO;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class MiniMagicTests
{
    [Theory]
    [InlineData("TestData.zip", ArchiveFileType.ZIP)]
    [InlineData("TestData.7z", ArchiveFileType.P7ZIP)]
    [InlineData("TestData.Tar", ArchiveFileType.TAR)]
    [InlineData("TestData.rar", ArchiveFileType.RAR5)]
    [InlineData("TestData.rar4", ArchiveFileType.RAR)]
    [InlineData("TestData.tar.bz2", ArchiveFileType.BZIP2)]
    [InlineData("TestData.tar.gz", ArchiveFileType.GZIP)]
    [InlineData("TestData.tar.xz", ArchiveFileType.XZ)]
    [InlineData("sysvbanner_1.0-17fakesync1_amd64.deb", ArchiveFileType.DEB)]
    [InlineData("TestData.a", ArchiveFileType.AR)]
    [InlineData("TestData.iso", ArchiveFileType.ISO_9660)]
    [InlineData("UdfTest.iso", ArchiveFileType.UDF)]
    [InlineData("TestData.vhdx", ArchiveFileType.VHDX)]
    [InlineData("TestData.wim", ArchiveFileType.WIM)]
    [InlineData("Empty.vmdk", ArchiveFileType.VMDK)]
    [InlineData("HfsSampleUDCO.dmg", ArchiveFileType.DMG)]
    [InlineData("EmptyFile.txt", ArchiveFileType.UNKNOWN)]
    public void TestMiniMagic(string fileName, ArchiveFileType expectedArchiveFileType)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", fileName);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // Test just based on the content
        var fileEntry = new FileEntry("NoName", fs);

        // We make sure the expected type matches and we have reset the stream
        Assert.Equal(expectedArchiveFileType, fileEntry.ArchiveType);
        Assert.Equal(0, fileEntry.Content.Position);

        // Should also work if the stream doesn't start at 0
        fileEntry.Content.Position = 10;
        Assert.Equal(expectedArchiveFileType, fileEntry.ArchiveType);
        Assert.Equal(10, fileEntry.Content.Position);
    }
}