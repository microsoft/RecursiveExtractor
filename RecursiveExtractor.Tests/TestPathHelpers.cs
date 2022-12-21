using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Tests;

public static class TestPathHelpers
{
    public static string GetFreshTestDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "RecursiveExtractorTests", Guid.NewGuid().ToString());
    }
}