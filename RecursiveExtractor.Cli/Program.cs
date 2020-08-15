using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.CST.RecursiveExtractor.Cli
{
    class Program
    {
		[Verb("extract", true, HelpText = "Add file contents to the index.")]
		public class ExtractOptions
		{
			[Option(HelpText = "The name of the Archive to extract.", Required = true)]
			public string Input { get; set; }

			[Option(HelpText = "The directory to extract to.", Default = ".")]
			public string Output { get; set; }

			//[Option("passwords", Required = false, HelpText = "Comma separated list of passwords to use.", Separator = ',')]
			//public IEnumerable<string>? Passwords { get; set; }

			[Option(HelpText = "List all files extracted.")]
			public bool Verbose { get; set; }
		}

		static int Main(string[] args)
		{

			return CommandLine.Parser.Default.ParseArguments<ExtractOptions>(args)
			  .MapResult(
				(ExtractOptions opts) => Extract(opts),
				errs => 1);
		}

		static int Extract(ExtractOptions options)
        {
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
				Directory.CreateDirectory(Path.Combine(options.Output,Path.GetDirectoryName(result.FullPath).TrimStart(Path.DirectorySeparatorChar)));
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
