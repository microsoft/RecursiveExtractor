using CommandLine;
using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Cli
{
	[Verb("extract", true, HelpText = "Add file contents to the index.")]
	public class ExtractCommandOptions
	{
		[Option(HelpText = "The name of the Archive to extract.", Required = true)]
		public string Input { get; set; } = string.Empty;

		[Option(HelpText = "The directory to extract to.", Default = ".")]
		public string Output { get; set; } = string.Empty;

        [Option("passwords", Required = false, HelpText = "Comma separated list of passwords to use.", Separator = ',')]
        public IEnumerable<string>? Passwords { get; set; }

        [Option(HelpText = "List all files extracted.")]
		public bool Verbose { get; set; }
	}
}
