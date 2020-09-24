using CommandLine;
using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Cli
{
	[Verb("extract", true, HelpText = "Extract an archive recursively.")]
	public class ExtractCommandOptions
	{
        [Option('i',"input", HelpText = "The name of the archive to extract.", Required = true)]
		public string Input { get; set; } = string.Empty;

		[Option('o', "output", HelpText = "The directory to extract to.", Default = ".")]
		public string Output { get; set; } = string.Empty;

        [Option('p', "passwords", Required = false, HelpText = "Comma-separated list of passwords to use.", Separator = ',')]
        public IEnumerable<string>? Passwords { get; set; }

        [Option('A', "allow-filters", Required = false, HelpText = "Comma-separated list of regular expressions. When set, files are ONLY written to disk if they match one of these filters.", Separator = ',')]
        public IEnumerable<string>? AllowFilters { get; set; }

        [Option('D', "deny-filters", Required = false, HelpText = "Comma-separated list of regular expressions. When set, files are NOT written to disk if they match one of these filters.", Separator = ',')]
        public IEnumerable<string>? DenyFilters { get; set; }

        [Option(HelpText = "Set logging to 'verbose'.")]
		public bool Verbose { get; set; }

        [Option(HelpText = "Set logging to 'debug'.")]
        public bool Debug { get; set; }

        [Option(HelpText = "Output the names of all files extracted.")]
        public bool PrintNames { get; set; }
    }
}
