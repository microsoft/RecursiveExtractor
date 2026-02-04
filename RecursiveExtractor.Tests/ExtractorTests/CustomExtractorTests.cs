using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Extractors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RecursiveExtractor.Tests.ExtractorTests;

[TestClass]
public class CustomExtractorTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// A simple test custom extractor that extracts files with a specific magic number
    /// For testing purposes, it recognizes files starting with "CUSTOM1"
    /// </summary>
    private class TestCustomExtractor : ICustomAsyncExtractor
    {
        private readonly Extractor context;
        private static readonly byte[] MAGIC_BYTES = System.Text.Encoding.ASCII.GetBytes("CUSTOM1");

        public TestCustomExtractor(Extractor ctx)
        {
            context = ctx;
        }

        public bool CanExtract(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < MAGIC_BYTES.Length)
            {
                return false;
            }

            var initialPosition = stream.Position;
            try
            {
                stream.Position = 0;
                var buffer = new byte[MAGIC_BYTES.Length];
                var bytesRead = stream.Read(buffer, 0, MAGIC_BYTES.Length);
                
                if (bytesRead == MAGIC_BYTES.Length && buffer.SequenceEqual(MAGIC_BYTES))
                {
                    return true;
                }
                return false;
            }
            finally
            {
                stream.Position = initialPosition;
            }
        }

        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            // For this test, we just return a synthetic file entry showing the custom extractor worked
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Extracted by TestCustomExtractor"));
            yield return new FileEntry("extracted_from_custom.txt", content, fileEntry);
        }

        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            // For this test, we just return a synthetic file entry showing the custom extractor worked
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Extracted by TestCustomExtractor"));
            yield return new FileEntry("extracted_from_custom.txt", content, fileEntry);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// A second test custom extractor that recognizes files starting with "CUSTOM2"
    /// </summary>
    private class SecondTestCustomExtractor : ICustomAsyncExtractor
    {
        private readonly Extractor context;
        private static readonly byte[] MAGIC_BYTES = System.Text.Encoding.ASCII.GetBytes("CUSTOM2");

        public SecondTestCustomExtractor(Extractor ctx)
        {
            context = ctx;
        }

        public bool CanExtract(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < MAGIC_BYTES.Length)
            {
                return false;
            }

            var initialPosition = stream.Position;
            try
            {
                stream.Position = 0;
                var buffer = new byte[MAGIC_BYTES.Length];
                var bytesRead = stream.Read(buffer, 0, MAGIC_BYTES.Length);
                
                if (bytesRead == MAGIC_BYTES.Length && buffer.SequenceEqual(MAGIC_BYTES))
                {
                    return true;
                }
                return false;
            }
            finally
            {
                stream.Position = initialPosition;
            }
        }

        public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Extracted by SecondTestCustomExtractor"));
            yield return new FileEntry("extracted_from_second_custom.txt", content, fileEntry);
        }

        public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
        {
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Extracted by SecondTestCustomExtractor"));
            yield return new FileEntry("extracted_from_second_custom.txt", content, fileEntry);
            await Task.CompletedTask;
        }
    }

    [TestMethod]
    public void Constructor_WithCustomExtractors_RegistersExtractors()
    {
        var customExtractor = new TestCustomExtractor(null!);
        var extractor = new Extractor(new[] { customExtractor });
        
        Assert.HasCount(1, extractor.CustomExtractors);
    }

    [TestMethod]
    public void Constructor_WithMultipleCustomExtractors_RegistersAll()
    {
        var customExtractor1 = new TestCustomExtractor(null!);
        var customExtractor2 = new SecondTestCustomExtractor(null!);
        var extractor = new Extractor(new ICustomAsyncExtractor[] { customExtractor1, customExtractor2 });
        
        Assert.HasCount(2, extractor.CustomExtractors);
    }

    [TestMethod]
    public void Constructor_WithNullInCollection_IgnoresNull()
    {
        var customExtractor = new TestCustomExtractor(null!);
        var extractor = new Extractor(new ICustomAsyncExtractor[] { customExtractor, null! });
        
        Assert.HasCount(1, extractor.CustomExtractors);
    }

    [TestMethod]
    public void Constructor_WithNullCollection_CreatesEmptyExtractor()
    {
        var extractor = new Extractor((IEnumerable<ICustomAsyncExtractor>)null!);
        
        Assert.IsEmpty(extractor.CustomExtractors);
    }

    [TestMethod]
    public void Extract_WithMatchingCustomExtractor_UsesCustomExtractor()
    {
        var customExtractor = new TestCustomExtractor(null!);
        var extractor = new Extractor(new[] { customExtractor });

        // Create a test file with the custom magic bytes
        var testData = System.Text.Encoding.ASCII.GetBytes("CUSTOM1 This is test data");
        var results = extractor.Extract("test.custom", testData).ToList();

        Assert.HasCount(1, results);
        Assert.AreEqual("extracted_from_custom.txt", results[0].Name);
        
        // Read the content to verify it was processed by our custom extractor
        using var reader = new StreamReader(results[0].Content);
        results[0].Content.Position = 0;
        var content = reader.ReadToEnd();
        Assert.AreEqual("Extracted by TestCustomExtractor", content);
    }

    [TestMethod]
    public async Task ExtractAsync_WithMatchingCustomExtractor_UsesCustomExtractor()
    {
        var customExtractor = new TestCustomExtractor(null!);
        var extractor = new Extractor(new[] { customExtractor });

        // Create a test file with the custom magic bytes
        var testData = System.Text.Encoding.ASCII.GetBytes("CUSTOM1 This is test data");
        var results = await extractor.ExtractAsync("test.custom", testData).ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.HasCount(1, results);
        Assert.AreEqual("extracted_from_custom.txt", results[0].Name);
        
        // Read the content to verify it was processed by our custom extractor
        using var reader = new StreamReader(results[0].Content);
        results[0].Content.Position = 0;
        var content = reader.ReadToEnd();
        Assert.AreEqual("Extracted by TestCustomExtractor", content);
    }

    [TestMethod]
    public void Extract_WithoutMatchingCustomExtractor_ReturnsOriginalFile()
    {
        var customExtractor = new TestCustomExtractor(null!);
        var extractor = new Extractor(new[] { customExtractor });

        // Create a test file that doesn't match the custom magic bytes
        var testData = System.Text.Encoding.ASCII.GetBytes("NOTCUSTOM This is test data");
        var results = extractor.Extract("test.txt", testData).ToList();

        // Should return the original file since no custom extractor matched
        Assert.HasCount(1, results);
        Assert.AreEqual("test.txt", results[0].Name);
        
        // Verify it's the original content
        using var reader = new StreamReader(results[0].Content);
        results[0].Content.Position = 0;
        var content = reader.ReadToEnd();
        Assert.AreEqual("NOTCUSTOM This is test data", content);
    }

    [TestMethod]
    public void Extract_MultipleCustomExtractors_UsesCorrectOne()
    {
        var extractor = new Extractor(new ICustomAsyncExtractor[] 
        { 
            new TestCustomExtractor(null!), 
            new SecondTestCustomExtractor(null!) 
        });

        // Test with first custom format
        var testData1 = System.Text.Encoding.ASCII.GetBytes("CUSTOM1 data");
        var results1 = extractor.Extract("test1.custom", testData1).ToList();
        Assert.HasCount(1, results1);
        Assert.AreEqual("extracted_from_custom.txt", results1[0].Name);

        // Test with second custom format
        var testData2 = System.Text.Encoding.ASCII.GetBytes("CUSTOM2 data");
        var results2 = extractor.Extract("test2.custom", testData2).ToList();
        Assert.HasCount(1, results2);
        Assert.AreEqual("extracted_from_second_custom.txt", results2[0].Name);
    }

    [TestMethod]
    public void Extract_NoCustomExtractors_ReturnsOriginalFile()
    {
        var extractor = new Extractor();

        // Don't add any custom extractors
        var testData = System.Text.Encoding.ASCII.GetBytes("CUSTOM1 This is test data");
        var results = extractor.Extract("test.custom", testData).ToList();

        // Should return the original file since no custom extractor is registered
        Assert.HasCount(1, results);
        Assert.AreEqual("test.custom", results[0].Name);
    }

    [TestMethod]
    public void Extract_CustomExtractorForKnownFormat_UsesBuiltInExtractor()
    {
        var customExtractor = new TestCustomExtractor(null!);
        var extractor = new Extractor(new[] { customExtractor });

        // Test with a real ZIP file - should use built-in extractor, not custom
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "TestDataArchives", "EmptyFile.txt.zip");
        if (File.Exists(path))
        {
            var results = extractor.Extract(path).ToList();
            
            // Should extract the ZIP normally, not use the custom extractor
            Assert.IsGreaterThan(results.Count, 0);
            Assert.IsTrue(results.Any(r => r.Name.Contains("EmptyFile")));
        }
    }
}
