using System;
using Microsoft.CST.RecursiveExtractor.Tests;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace RecursiveExtractor.Tests.ExtractorTests;

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

    public void Dispose()
    {
        TestPathHelpers.DeleteTestDirectory();
    }
}