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
    public class RecursiveExtractorClient
    {
		public static int Main(string[] args)
		{            
            return CommandLine.Parser.Default.ParseArguments<ExtractCommandOptions>(args)
			  .MapResult(
				(ExtractCommandOptions opts) => ExtractCommand(opts),
				errs => 1);
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
                Parallel = true,
                RawExtensions = options.RawExtensions
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
            var allowRegexes = options.AllowFilters?.Select(x => new Regex(x)) ?? Array.Empty<Regex>();
            var denyRegexes = options.DenyFilters?.Select(x => new Regex(x)) ?? Array.Empty<Regex>();
            extractor.ExtractToDirectory(options.Output, options.Input, extractorOptions, allowRegexes, denyRegexes, options.PrintNames);

            return 0;
        }
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}
