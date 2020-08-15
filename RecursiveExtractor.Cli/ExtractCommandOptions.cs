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

        [Option(HelpText = "Set logging to Verbose.")]
		public bool Verbose { get; set; }

        [Option(HelpText = "Set logging to Debug.")]
        public bool Debug { get; set; }

        [Option(HelpText = "Print names of all extracted files.")]
        public bool PrintNames { get; set; }
    }
}
