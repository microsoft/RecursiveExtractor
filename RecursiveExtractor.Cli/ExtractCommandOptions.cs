using CommandLine;
using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Cli
{
	[Verb("extract", true, HelpText = "Add file contents to the index.")]
	public class ExtractCommandOptions
	{
        [Option('i',"input",HelpText = "The name of the Archive to extract.", Required = true)]
		public string Input { get; set; } = string.Empty;

		[Option('o', "output", HelpText = "The directory to extract to.", Default = ".")]
		public string Output { get; set; } = string.Empty;

        [Option('p', "passwords", Required = false, HelpText = "Comma separated list of passwords to use.", Separator = ',')]
        public IEnumerable<string>? Passwords { get; set; }

        [Option('A', "allow-filters", Required = false, HelpText = "Comma separated list of regexes.  When set, files are only written to disc if they match one of these filters.", Separator = ',')]
        public IEnumerable<string>? AllowFilters { get; set; }

        [Option('D', "deny-filters", Required = false, HelpText = "Comma separated list of regexes.  When set, files are not written to disc if they match one of these filters.", Separator = ',')]
        public IEnumerable<string>? DenyFilters { get; set; }

        [Option(HelpText = "Set logging to Verbose.")]
		public bool Verbose { get; set; }

        [Option(HelpText = "Set logging to Debug.")]
        public bool Debug { get; set; }

        [Option(HelpText = "Print names of all extracted files.")]
        public bool PrintNames { get; set; }
    }
}
