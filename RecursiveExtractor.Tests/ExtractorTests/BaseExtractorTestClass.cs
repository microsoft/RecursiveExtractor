using System;
using Microsoft.CST.RecursiveExtractor.Tests;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

/// <summary>
/// Shared fixture that cleans up temp directories once after all tests in a class complete.
/// </summary>
public class TestCleanupFixture : IDisposable
{
    public void Dispose()
    {
        TestPathHelpers.DeleteTestDirectory();
    }
}

public class BaseExtractorTestClass : IClassFixture<TestCleanupFixture>
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
}