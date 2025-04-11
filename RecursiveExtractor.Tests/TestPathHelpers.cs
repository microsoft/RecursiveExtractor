using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.CST.RecursiveExtractor.Tests;

public static class TestPathHelpers
{
    public const string TestTempFolderName = "RE_Tests";

    public static string TestDirectoryPath => Path.Combine(Path.GetTempPath(), TestTempFolderName);
    
    public static string GetFreshTestDirectory()
    {
        return Path.Combine(TestDirectoryPath, FileEntry.SanitizePath(DateTime.Now.ToString("yyMMdd_hhmmss_fffffff")));
    }

    public static void DeleteTestDirectory()
    {
        try
        {
            Directory.Delete(Path.Combine(TestDirectoryPath), true);
        }
        catch (DirectoryNotFoundException)
        {
            // Not an error. Not every test makes the folder.
        }
        catch (Exception e)
        {
            // Throwing the exception up may cause tests to fail due to file system oddness so just log
            Logger.Warn("Failed to delete Test Working Directory at {directory}", TestDirectoryPath);
        }
    }
    
    static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
}