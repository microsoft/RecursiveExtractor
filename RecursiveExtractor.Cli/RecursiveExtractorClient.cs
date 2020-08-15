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

			if (!Directory.Exists(options.Output))
            {
				Directory.CreateDirectory(options.Output);
            }
			var extractor = new Extractor();
			var extractorOptions = new ExtractorOptions()
			{
				ExtractSelfOnFail = true,
				Parallel = true,
			};
			//if (options.Passwords.Any())
   //         {
			//	extractorOptions.Passwords = new Dictionary<Regex, List<string>>()
			//	{
			//		{
			//			new Regex(".*",RegexOptions.Compiled),
			//			options.Passwords.ToList()
			//		}
			//	};

			//}
			foreach(var result in extractor.ExtractFile(options.Input, extractorOptions))
            {
				Directory.CreateDirectory(Path.Combine(options.Output,Path.GetDirectoryName(result.FullPath)?.TrimStart(Path.DirectorySeparatorChar) ?? string.Empty));
				using var fs = new FileStream(Path.Combine(options.Output,result.FullPath), FileMode.Create);
				result.Content.CopyTo(fs);
				if (options.Verbose)
				{
					Console.WriteLine("Extracted {0}.", result.FullPath);
				}
			}
			return 0;
        }
	}
}
