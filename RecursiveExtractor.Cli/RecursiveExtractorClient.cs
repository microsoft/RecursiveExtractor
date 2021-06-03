using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NLog.Config;
using NLog.Targets;
namespace Microsoft.CST.RecursiveExtractor.Cli
{
    public static class RecursiveExtractorClient
    {
		public static int Main(string[] args)
		{
            return CommandLine.Parser.Default.ParseArguments<ExtractCommandOptions>(args)
			  .MapResult(
				(ExtractCommandOptions opts) => ExtractCommand(opts),
				_ => 1);
		}

        public static int ExtractCommand(ExtractCommandOptions options)
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget
            {
                Name = "console",
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}",
            };
            if (options.Verbose)
            {
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget, "*");
            }
            else if (options.Debug)
            {
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget, "*");
            }
            else
            {
                config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget, "*");
            }

            LogManager.Configuration = config;

            var extractor = new Extractor();
            var extractorOptions = new ExtractorOptions()
            {
                ExtractSelfOnFail = true,
                Parallel = !options.SingleThread,
                RawExtensions = options.RawExtensions ?? Array.Empty<string>(),
                Recurse = !options.DisableRecursion,
                AllowFilters = options.AllowFilters ?? Array.Empty<string>(),
                DenyFilters = options.DenyFilters ?? Array.Empty<string>()
            };
            if (options.Passwords?.Any() ?? false)
            {
                extractorOptions.Passwords = new Dictionary<Regex, List<string>>()
                {
                    {
                        new Regex(".*",RegexOptions.Compiled),
                        options.Passwords.ToList()
                    }
                };
            }
            var exitCode = ExtractionStatusCode.Ok;
            try
            {
                exitCode = extractor.ExtractToDirectory(options.Output, options.Input, extractorOptions, options.PrintNames);
            }
            catch(Exception e)
            {
                Logger.Error($"Exception while extracting. {e.GetType()}:{e.Message} ({e.StackTrace})");
                exitCode = ExtractionStatusCode.Failure;
            }

            return (int)exitCode;
        }
        private readonly static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}
