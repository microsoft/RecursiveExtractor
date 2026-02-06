using Microsoft.CST.RecursiveExtractor.Tests;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;

namespace RecursiveExtractor.Tests.ExtractorTests;

/// <summary>
/// XUnit test fixture class for extractor tests. Sets up logging and test directories. Tests should use this class as a fixture via IClassFixture&lt;BaseExtractorTestClass&gt; to get the benefits of the setup and teardown.
/// </summary>
public class BaseExtractorTestClass : IDisposable
{
    protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    static BaseExtractorTestClass()
    {
        var config = new LoggingConfiguration();
        var consoleTarget = new ConsoleTarget
        {
            Name = "console",
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}",
        };
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget, "*");

        LogManager.Configuration = config;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        TestPathHelpers.DeleteTestDirectory();
        GC.SuppressFinalize(this);
    }
}