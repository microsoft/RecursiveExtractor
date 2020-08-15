// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CST.RecursiveExtractor.Cli;

namespace Microsoft.CST.RecursiveExtractor.Tests
{
    [TestClass]
    public class ExtractorCliTests
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
        [DataRow("Nested.Zip", 26 * 8 + 1)] // there's one extra metadata file in there
        public void ExtractArchive(string fileName, int expectedNumFiles = 26)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
            RecursiveExtractorClient.Extract(new ExtractCommandOptions() { Input = path, Output = directory, Verbose = true });
            if (Directory.Exists(directory))
            {
                var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).ToList();
                Assert.IsTrue(files.Count == expectedNumFiles);
                Directory.Delete(directory, true);
            }
            else
            {
                Assert.IsTrue(expectedNumFiles == 0);
            }
        }

        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}