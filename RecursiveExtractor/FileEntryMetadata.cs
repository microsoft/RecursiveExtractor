// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.IO;

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// Metadata about a file extracted from an archive, such as permissions and ownership.
    /// Properties are nullable to indicate when the metadata was not available from the archive format.
    /// </summary>
    public class FileEntryMetadata
    {
        /// <summary>
        /// The Unix file mode (permissions) as an integer (e.g., 0x1FF for 0777, 0x1ED for 0755).
        /// Null if not available from the archive format.
        /// </summary>
        public long? Mode { get; set; }

        /// <summary>
        /// Whether the file has any executable permission bits set (owner, group, or other).
        /// Derived from <see cref="Mode"/> when available, otherwise null.
        /// </summary>
        public bool? IsExecutable => Mode.HasValue ? (Mode.Value & 0x49) != 0 : null; // 0x49 = 0111 in octal

        /// <summary>
        /// Whether the SetUID bit is set on this file.
        /// Derived from <see cref="Mode"/> when available, otherwise null.
        /// </summary>
        public bool? IsSetUid => Mode.HasValue ? (Mode.Value & 0x800) != 0 : null; // 04000 in octal

        /// <summary>
        /// Whether the SetGID bit is set on this file.
        /// Derived from <see cref="Mode"/> when available, otherwise null.
        /// </summary>
        public bool? IsSetGid => Mode.HasValue ? (Mode.Value & 0x400) != 0 : null; // 02000 in octal

        /// <summary>
        /// The User ID (UID) of the file owner.
        /// Null if not available from the archive format.
        /// </summary>
        public long? Uid { get; set; }

        /// <summary>
        /// The Group ID (GID) of the file owner.
        /// Null if not available from the archive format.
        /// </summary>
        public long? Gid { get; set; }

        /// <summary>
        /// The Windows file attributes (e.g., ReadOnly, Hidden, System, Archive).
        /// Available for NTFS, FAT, and WIM file systems.
        /// Null if not available from the archive format.
        /// </summary>
        public FileAttributes? FileAttributes { get; set; }

        /// <summary>
        /// The NTFS security descriptor in SDDL (Security Descriptor Definition Language) format.
        /// Available for NTFS and WIM file systems that implement <c>IWindowsFileSystem</c>.
        /// Null if not available from the archive format.
        /// </summary>
        public string? SecurityDescriptorSddl { get; set; }
    }
}
