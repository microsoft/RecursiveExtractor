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
        [InlineData("a/file/with:colon.name", "a/file/with_colon.name")]
        [InlineData("a/folder:with/colon.name", "a/folder_with/colon.name")]

        public void TestSanitizePathLinux(string linuxInputPath, string expectedLinuxPath)
        {
            var entry = new FileEntry(linuxInputPath, Stream.Null);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.Equal(expectedLinuxPath, entry.GetSanitizedPath());
            }
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}