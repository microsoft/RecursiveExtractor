// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor.Tests
{
    [TestClass]
    public class SanitizePathTests
    {
        [DataTestMethod]
        [DataRow("a\\file\\with:colon.name", "a\\file\\with_colon.name")]
        [DataRow("a\\folder:with\\colon.name", "a\\folder_with\\colon.name")]

        public void TestSanitizePathWindows(string windowsInputPath, string expectedWindowsPath)
        {
            var entry = new FileEntry(windowsInputPath, Stream.Null);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(expectedWindowsPath, entry.GetSanitizedPath());
            }
        }
        
        [DataTestMethod]
        [DataRow("a/file/with:colon.name", "a/file/with_colon.name")]
        [DataRow("a/folder:with/colon.name", "a/folder_with/colon.name")]

        public void TestSanitizePathLinux(string linuxInputPath, string expectedLinuxPath)
        {
            var entry = new FileEntry(linuxInputPath, Stream.Null);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(expectedLinuxPath, entry.GetSanitizedPath());
            }
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}