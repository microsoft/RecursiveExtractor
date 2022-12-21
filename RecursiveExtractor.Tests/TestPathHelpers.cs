using System;
using System.Globalization;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor.Tests;

public static class TestPathHelpers
{
    public const string TestTempFolderName = "RE_Tests";
    
    public static string GetFreshTestDirectory()
    {
        return Path.Combine(Path.GetTempPath(), TestTempFolderName, FileEntry.SanitizePath(DateTime.Now.ToString("yyMMdd_hhmmss_fffffff")));
    }

    public static void DeleteTestDirectory()
    {
        Directory.Delete(Path.Combine(Path.GetTempPath(), TestTempFolderName), true);
    }
}