using Microsoft.CST.RecursiveExtractor.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace RecursiveExtractor.Tests.ExtractorTests;

public class BaseExtractorTestClass
{
    [ClassCleanup]
    public static void ClassCleanup()
    {
        TestPathHelpers.DeleteTestDirectory();
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
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
    protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
}