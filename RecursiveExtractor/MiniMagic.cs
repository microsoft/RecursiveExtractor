// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    ///     ArchiveTypes represent the kinds of archive files that this module can process.
    /// </summary>
    public enum ArchiveFileType
    {
        /// <summary>
        /// A file not of any of the known types.
        /// </summary>
        UNKNOWN = 0,
        /// <summary>
        /// A zip formatted file. <see cref="Extractors.ZipExtractor"/>
        /// </summary>
        ZIP = 1,
        /// <summary>
        /// A tar formatted file. <see cref="Extractors.TarExtractor"/>
        /// </summary>
        TAR = 2,
        /// <summary>
        /// An xzip formatted file. <see cref="Extractors.XzExtractor"/>
        /// </summary>
        XZ = 3,
        /// <summary>
        /// A gzip formatted file. <see cref="Extractors.GzipExtractor"/>
        /// </summary>
        GZIP = 4,
        /// <summary>
        /// A bzip2 formatted file. <see cref="Extractors.BZip2Extractor"/>
        /// </summary>
        BZIP2 = 5,
        /// <summary>
        /// A Rar4 formatted file. <see cref="Extractors.RarExtractor"/>
        /// </summary>
        RAR = 6,
        /// <summary>
        /// A Rar5 formatted file. <see cref="Extractors.RarExtractor"/>
        /// </summary>
        RAR5 = 7,
        /// <summary>
        /// An 7zip formatted file. <see cref="Extractors.SevenZipExtractor"/>
        /// </summary>
        P7ZIP = 8,
        /// <summary>
        /// An deb formatted file. <see cref="Extractors.DebExtractor"/>
        /// </summary>
        DEB = 9,
        /// <summary>
        /// An ar formatted file. <see cref="Extractors.GnuArExtractor"/>
        /// </summary>
        AR = 10,
        /// <summary>
        /// An iso disc image. <see cref="Extractors.IsoExtractor"/>
        /// </summary>
        ISO_9660 = 11,
        /// <summary>
        /// An UDF disc image. <see cref="Extractors.UdfExtractor"/>
        /// </summary>
        UDF = 12,
        /// <summary>
        /// A VHDX disc image. <see cref="Extractors.VhdxExtractor"/>
        /// </summary>
        VHDX = 13,
        /// <summary>
        /// A VHD disc image. <see cref="Extractors.VhdExtractor"/>
        /// </summary>
        VHD = 14,
        /// <summary>
        /// A wim disc image. <see cref="Extractors.WimExtractor"/>
        /// </summary>
        WIM = 15,
        /// <summary>
        /// An vmdk disc image. <see cref="Extractors.VmdkExtractor"/>
        /// </summary>
        VMDK = 16,
        /// <summary>
        /// A DMG disc image.
        /// </summary>
        DMG = 17,
        /// <summary>
        /// An ARJ compressed archive. <see cref="Extractors.ArjExtractor"/>
        /// </summary>
        ARJ = 19,
        /// <summary>
        /// An ARC compressed archive. <see cref="Extractors.ArcExtractor"/>
        /// </summary>
        ARC = 20,
        /// <summary>
        /// An ACE compressed archive. <see cref="Extractors.AceExtractor"/>
        /// </summary>
        ACE = 21,
        /// <summary>
        /// Unused.
        /// </summary>
        INVALID = 18,
    }

    /// <summary>
    ///     MiniMagic is a tiny implementation of a file type identifier based on binary signatures.
    /// </summary>
    public static class MiniMagic
    {
        /// <summary>
        /// Detect the type of a file given its path on disk.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static ArchiveFileType DetectFileType(string filename)
        {
#pragma warning disable SEC0116 // Path Tampering Unvalidated File Path
            using var fs = new FileStream(filename, FileMode.Open);
#pragma warning restore SEC0116 // Path Tampering Unvalidated File Path
            return DetectFileType(fs);
        }

        /// <summary>
        /// Detect the type of a file given a stream of its contents.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <returns></returns>
        public static ArchiveFileType DetectFileType(Stream fileStream)
        {
            if (fileStream == null)
            {
                return ArchiveFileType.UNKNOWN;
            }
            var initialPosition = fileStream.Position;
            var buffer = new byte[14];
            // DMG format uses the magic value 'koly' at the start of the 512 byte footer at the end of the file
            // Due to compression used, needs to be first or can be misidentified as other formats
            // https://newosxbook.com/DMG.html
            if (fileStream.Length > 512)
            {
                var dmgFooterMagic = new byte[] { 0x6b, 0x6f, 0x6c, 0x79 };
                fileStream.Position = fileStream.Length - 0x200; // Footer position
                fileStream.ReadExactly(buffer, 0, 4);
                fileStream.Position = initialPosition;

                if (dmgFooterMagic.SequenceEqual(buffer[0..4]))
                {
                    return ArchiveFileType.DMG;
                }
            }

            var bytesRead = 0;
            if (fileStream.Length >= 2)
            {
                var toRead = (int)Math.Min(fileStream.Length, buffer.Length);
                fileStream.Position = 0;
                fileStream.ReadExactly(buffer, 0, toRead);
                fileStream.Position = initialPosition;
                bytesRead = toRead;

                if (buffer[0] == 0x1F && buffer[1] == 0x8B)
                {
                    return ArchiveFileType.GZIP;
                }
                // ARJ archive header starts with 0x60, 0xEA
                if (buffer[0] == 0x60 && buffer[1] == 0xEA)
                {
                    return ArchiveFileType.ARJ;
                }
                // ARC archive: marker byte 0x1A, then compression method (valid: 0x01-0x09 or 0x7F)
                if (buffer[0] == 0x1A && ((buffer[1] >= 0x01 && buffer[1] <= 0x09) || buffer[1] == 0x7F))
                {
                    return ArchiveFileType.ARC;
                }
            }

            if (bytesRead >= 3)
            {
                if (buffer[0] == 0x42 && buffer[1] == 0x5A && buffer[2] == 0x68)
                {
                    return ArchiveFileType.BZIP2;
                }
            }

            if (bytesRead >= 4)
            {
                if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
                {
                    return ArchiveFileType.ZIP;
                }
            }

            if (bytesRead >= 6)
            {
                if (buffer[0] == 0xFD && buffer[1] == 0x37 && buffer[2] == 0x7A && buffer[3] == 0x58 && buffer[4] == 0x5A && buffer[5] == 0x00)
                {
                    return ArchiveFileType.XZ;
                }
                if (buffer[0] == 0x37 && buffer[1] == 0x7A && buffer[2] == 0xBC && buffer[3] == 0xAF && buffer[4] == 0x27 && buffer[5] == 0x1C)
                {
                    return ArchiveFileType.P7ZIP;
                }
            }

            if (bytesRead >= 7)
            {
                if (buffer[0] == 0x52 && buffer[1] == 0x61 && buffer[2] == 0x72 && buffer[3] == 0x21 && buffer[4] == 0x1A && buffer[5] == 0x07 && buffer[6] == 0x00)
                {
                    return ArchiveFileType.RAR;
                }
            }

            if (bytesRead >= 8)
            {
                if (buffer[0] == 0x52 && buffer[1] == 0x61 && buffer[2] == 0x72 && buffer[3] == 0x21 && buffer[4] == 0x1A && buffer[5] == 0x07 && buffer[6] == 0x01 && buffer[7] == 0x00)
                {
                    return ArchiveFileType.RAR5;
                }
                if (Encoding.ASCII.GetString(buffer[0..8]) == "MSWIM\0\0\0" || Encoding.ASCII.GetString(buffer[0..8]) == "WLPWM\0\0\0")
                {
                    return ArchiveFileType.WIM;
                }
                if (Encoding.ASCII.GetString(buffer[0..4]) == "KDMV")
                {
                    fileStream.Position = 512;
                    var secondToken = new byte[21];
                    fileStream.ReadExactly(secondToken, 0, 21);
                    fileStream.Position = initialPosition;

                    if (Encoding.ASCII.GetString(secondToken) == "# Disk DescriptorFile")
                    {
                        return ArchiveFileType.VMDK;
                    }
                }
                // some kind of unix Archive https://en.wikipedia.org/wiki/Ar_(Unix)
                if (buffer[0] == 0x21 && buffer[1] == 0x3c && buffer[2] == 0x61 && buffer[3] == 0x72 && buffer[4] == 0x63 && buffer[5] == 0x68 && buffer[6] == 0x3e)
                {
                    // .deb https://manpages.debian.org/unstable/dpkg-dev/deb.5.en.html
                    fileStream.Position = 68;
                    fileStream.ReadExactly(buffer, 0, 4);
                    fileStream.Position = initialPosition;

                    var encoding = new ASCIIEncoding();
                    if (encoding.GetString(buffer[0..4]) == "2.0\n")
                    {
                        return ArchiveFileType.DEB;
                    }
                    else
                    {
                        var headerBuffer = new byte[60];

                        // Created by GNU ar https://en.wikipedia.org/wiki/Ar_(Unix)#System_V_(or_GNU)_variant
                        fileStream.Position = 8;
                        fileStream.ReadExactly(headerBuffer, 0, 60);
                        fileStream.Position = initialPosition;

                        // header size in bytes
                        if (int.TryParse(Encoding.ASCII.GetString(headerBuffer[48..58]), out var size) && size > 0)
                        {
                            // Defined ending characters for a header
                            if (headerBuffer[58] == '`' && headerBuffer[59] == '\n')
                            {
                                return ArchiveFileType.AR;
                            }
                        }
                    }
                }
                // https://winprotocoldoc.blob.core.windows.net/productionwindowsarchives/MS-VHDX/%5bMS-VHDX%5d.pdf
                if (Encoding.UTF8.GetString(buffer[0..8]).Equals("vhdxfile"))
                {
                    return ArchiveFileType.VHDX;
                }
            }

            // ACE archive: signature "**ACE**" at offset 7
            if (bytesRead >= 14 && buffer[7] == 0x2A && buffer[8] == 0x2A && buffer[9] == 0x41 && buffer[10] == 0x43 && buffer[11] == 0x45 && buffer[12] == 0x2A && buffer[13] == 0x2A)
            {
                return ArchiveFileType.ACE;
            }

            if (fileStream.Length >= 262)
            {
                fileStream.Position = 257;
                fileStream.ReadExactly(buffer, 0, 5);
                fileStream.Position = initialPosition;

                if (buffer[0] == 0x75 && buffer[1] == 0x73 && buffer[2] == 0x74 && buffer[3] == 0x61 && buffer[4] == 0x72)
                {
                    return ArchiveFileType.TAR;
                }
            }

            // ISO Format https://en.wikipedia.org/wiki/ISO_9660#Overall_structure Reserved space + 1 header
            if (fileStream.Length > 32768 + 2048)
            {
                fileStream.Position = 32769;
                fileStream.ReadExactly(buffer, 0, 5);
                fileStream.Position = initialPosition;

                if (buffer[0] == 'C' && buffer[1] == 'D' && buffer[2] == '0' && buffer[3] == '0' && buffer[4] == '1')
                {
                    return ArchiveFileType.ISO_9660;
                }
                if (buffer[0] == 'B' && buffer[1] == 'E' && buffer[2] == 'A' && buffer[3] == '0' && buffer[4] == '1')
                {
                    return ArchiveFileType.UDF;
                }
            }

            //https://www.microsoft.com/en-us/download/details.aspx?id=23850 - 'Hard Disk Footer Format'
            // Unlike other formats the magic string is stored in the footer, which is either the last 511 or 512 bytes
            // The magic string is Magic string "conectix" (63 6F 6E 65 63 74 69 78)
            if (fileStream.Length > 512)
            {
                var vhdFooterCookie = new byte[] { 0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78 };

                fileStream.Position = fileStream.Length - 0x200; // Footer position
                fileStream.ReadExactly(buffer, 0, 8);
                fileStream.Position = initialPosition;

                if (vhdFooterCookie.SequenceEqual(buffer[0..8]))
                {
                    return ArchiveFileType.VHD;
                }

                fileStream.Position = fileStream.Length - 0x1FF; //If created on legacy platform footer is 511 bytes instead
                fileStream.ReadExactly(buffer, 0, 8);
                fileStream.Position = initialPosition;

                if (vhdFooterCookie.SequenceEqual(buffer[0..8]))
                {
                    return ArchiveFileType.VHD;
                }
            }
            return ArchiveFileType.UNKNOWN;
        }

        /// <summary>
        ///     Detects the type of a file given a fileEntry
        /// </summary>
        /// <param name="fileEntry"> FileEntry containing the file data. </param>
        /// <returns>The ArchiveFileType detected</returns>
        public static ArchiveFileType DetectFileType(FileEntry fileEntry) => DetectFileType(fileEntry?.Content ?? new MemoryStream());
    }
}
