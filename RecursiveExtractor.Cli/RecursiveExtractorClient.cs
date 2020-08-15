using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.CST.RecursiveExtractor.Cli
{
    public class RecursiveExtractorClient
    {
		public static int Main(string[] args)
		{

			return CommandLine.Parser.Default.ParseArguments<ExtractCommandOptions>(args)
			  .MapResult(
				(ExtractCommandOptions opts) => Extract(opts),
				errs => 1);
		}

		public static int Extract(ExtractCommandOptions options)
        {
			var extractor = new Extractor();
			var extractorOptions = new ExtractorOptions()
			{
				ExtractSelfOnFail = true,
				Parallel = true,
			};
            if (options.Passwords.Any())
            {
                extractorOptions.Passwords = new Dictionary<Regex, List<string>>()
                {
                    {
                        new Regex(".*",RegexOptions.Compiled),
                        options.Passwords.ToList()
                    }
                };
            }
            foreach (var result in extractor.ExtractFile(options.Input, extractorOptions))
            {
                var targetPath = Path.Combine(options.Output, result.FullPath);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    using var fs = new FileStream(targetPath, FileMode.Create);
                    result.Content.CopyTo(fs);
                    if (options.Verbose)
                    {
                        Console.WriteLine("Extracted {0}.", result.FullPath);
                    }
                }
				catch(Exception e)
                {
                    Logger.Fatal(e, "Failed to create file at {0}", targetPath);
                }
            }
			return 0;
        }
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}
