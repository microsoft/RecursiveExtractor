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
            if (options.Verbose) {
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
            foreach (var result in extractor.ExtractFile(options.Input, extractorOptions))
            {
                var skip = false;
                foreach(var allowRegex in allowRegexes)
                {
                    if (!allowRegex.IsMatch(result.FullPath))
                    {
                        skip = true;
                        break;
                    }
                }
                if (skip) { continue; }
                foreach(var denyRegex in denyRegexes)
                {
                    if (denyRegex.IsMatch(result.FullPath)) 
                    {
                        skip = true;
                        break;
                    }                
                }
                if (skip) { continue; }
                var targetPath = Path.Combine(options.Output, result.FullPath);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    using var fs = new FileStream(targetPath, FileMode.Create);
                    result.Content.CopyTo(fs);
                    if (options.PrintNames)
                    {
                        Console.WriteLine("Extracted {0}.", result.FullPath);
                    }
                    Logger.Trace("Extracted {0}", result.FullPath);
                }
                catch (Exception e)
                {
                    Logger.Fatal(e, "Failed to create file at {0}.", targetPath);
                }
            }
			return 0;
        }
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}
