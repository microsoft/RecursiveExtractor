// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.RecursiveExtractor;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace RecursiveExtractor.Tests
{
    public class SanitizePathTests
    {
        [Theory]
        [InlineData("a\\file\\with:colon.name", "a\\file\\with_colon.name")]
        [InlineData("a\\folder:with\\colon.name", "a\\folder_with\\colon.name")]

        public void TestSanitizePathWindows(string windowsInputPath, string expectedWindowsPath)
        {
            var entry = new FileEntry(windowsInputPath, Stream.Null);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal(expectedWindowsPath, entry.GetSanitizedPath());
            }
        }
        
        [Theory]
        [InlineData("a/file/with:colon.name", "a/file/with:colon.name")]
        [InlineData("a/folder:with/colon.name", "a/folder:with/colon.name")]

        public void TestSanitizePathLinux(string linuxInputPath, string expectedLinuxPath)
        {
            var entry = new FileEntry(linuxInputPath, Stream.Null);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.Equal(expectedLinuxPath, entry.GetSanitizedPath());
            }
        }

        /// <summary>
        /// ZipSlipSanitize should strip leading slashes to prevent absolute path traversal.
        /// On any OS, a Unix-style absolute path like "/etc/passwd" must become relative.
        /// </summary>
        [Theory]
        [InlineData("/etc/passwd", "etc/passwd")]
        [InlineData("/tmp/evil.txt", "tmp/evil.txt")]
        [InlineData("///triple/leading", "triple/leading")]
        [InlineData("/", "")]
        public void TestZipSlipSanitize_AbsoluteUnixPaths(string input, string expected)
        {
            // Normalize expected to the current OS separator
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// ZipSlipSanitize should strip Windows drive letter roots.
        /// </summary>
        [Theory]
        [InlineData("C:\\Windows\\System32\\evil.dll", "Windows/System32/evil.dll")]
        [InlineData("D:/data/file.txt", "data/file.txt")]
        [InlineData("C:\\", "")]
        [InlineData("C:/", "")]
        [InlineData("a:file.txt", "a:file.txt")]
        public void TestZipSlipSanitize_AbsoluteWindowsPaths(string input, string expected)
        {
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// ZipSlipSanitize should recursively strip nested roots that are exposed after each strip.
        /// A single pass is insufficient for crafted paths like "D:\C:\" (stripping D: exposes \C:\),
        /// or paths starting with multiple separators like "\\C:\" (stripping both leading separators
        /// exposes C:\), or "../C:\file" where removing ".." exposes C:\file.
        /// </summary>
        [Theory]
        [InlineData("D:\\C:\\file.txt", "file.txt")]
        [InlineData("\\\\C:\\file.txt", "file.txt")]
        [InlineData("..\\C:\\file.txt", "file.txt")]
        [InlineData("D:/C:/file.txt", "file.txt")]
        [InlineData("D:\\C:\\D:\\file.txt", "file.txt")]
        public void TestZipSlipSanitize_NestedRoots(string input, string expected)
        {
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// ZipSlipSanitize should strip ".." directory traversal components.
        /// </summary>
        [Theory]
        [InlineData("foo/../../../etc/passwd", "foo/etc/passwd")]
        [InlineData("../secret.txt", "secret.txt")]
        [InlineData("a/b/../../c", "a/b/c")]
        public void TestZipSlipSanitize_DotDotTraversal(string input, string expected)
        {
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// ZipSlipSanitize should handle combined absolute + traversal attacks.
        /// After ".." is stripped, exposed leading separators are also stripped.
        /// </summary>
        [Theory]
        [InlineData("/../../etc/passwd", "etc/passwd")]
        [InlineData("C:\\..\\..\\Windows\\evil.dll", "Windows/evil.dll")]
        [InlineData("/../../../tmp/evil", "tmp/evil")]
        public void TestZipSlipSanitize_CombinedAbsoluteAndTraversal(string input, string expected)
        {
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// ZipSlipSanitize should collapse double separators that result from stripping.
        /// </summary>
        [Theory]
        [InlineData("a/../b", "a/b")]
        [InlineData("a/../../b", "a/b")]
        public void TestZipSlipSanitize_CollapsesDoubleSeparators(string input, string expected)
        {
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Safe relative paths should pass through unmodified.
        /// Filenames containing ".." as a substring (not a path segment) must be preserved.
        /// </summary>
        [Theory]
        [InlineData("normal/path/file.txt", "normal/path/file.txt")]
        [InlineData("file.txt", "file.txt")]
        [InlineData("a/b/c/d.txt", "a/b/c/d.txt")]
        [InlineData("file..txt", "file..txt")]
        [InlineData("my..archive/data..bin", "my..archive/data..bin")]
        [InlineData("a/./b", "a/b")]
        [InlineData("a:file.txt", "a:file.txt")]
        [InlineData("a:folder/file.txt", "a:folder/file.txt")]
        public void TestZipSlipSanitize_SafePathsUnchanged(string input, string expected)
        {
            expected = expected.Replace('/', Path.DirectorySeparatorChar);
            var result = FileEntry.ZipSlipSanitize(input);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// FileEntry.FullPath should never be absolute when constructed with a parent,
        /// even if the entry name is an absolute path.
        /// </summary>
        [Fact]
        public void TestFileEntry_AbsoluteEntryName_BecomesRelative()
        {
            var parent = new FileEntry("archive.zip", Stream.Null);
            var child = new FileEntry("/etc/cron.d/evil", Stream.Null, parent);
            Assert.False(Path.IsPathRooted(child.FullPath),
                $"FullPath should be relative but was: {child.FullPath}");
            AssertNoTraversalSegments(child.FullPath);
        }

        /// <summary>
        /// FileEntry.FullPath should not contain ".." path segments even when the entry name has traversal.
        /// </summary>
        [Fact]
        public void TestFileEntry_TraversalEntryName_Sanitized()
        {
            var parent = new FileEntry("archive.tar", Stream.Null);
            var child = new FileEntry("../../../etc/passwd", Stream.Null, parent);
            AssertNoTraversalSegments(child.FullPath);
        }

        /// <summary>
        /// Backslash-rooted paths should also be made relative.
        /// </summary>
        [Fact]
        public void TestFileEntry_BackslashRooted_BecomesRelative()
        {
            var parent = new FileEntry("archive.zip", Stream.Null);
            var child = new FileEntry("\\Windows\\System32\\evil.dll", Stream.Null, parent);
            Assert.False(Path.IsPathRooted(child.FullPath),
                $"FullPath should be relative but was: {child.FullPath}");
        }

        /// <summary>
        /// Assert that no path segment is exactly ".." (ignoring ".." as a substring in filenames like "file..txt").
        /// </summary>
        private static void AssertNoTraversalSegments(string path)
        {
            var segments = path.Split(Path.DirectorySeparatorChar);
            Assert.DoesNotContain("..", segments);
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}